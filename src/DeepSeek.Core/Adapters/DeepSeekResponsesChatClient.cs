using DeepSeek.Core.Models;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DeepSeek.Core.Adapters;

/// <summary>
/// Microsoft.Extensions.AI adapter backed by DeepSeek's OpenAI-compatible Responses API.
/// </summary>
public sealed class DeepSeekResponsesChatClient : IChatClient
{
    private readonly DeepSeekClient _client;

    public DeepSeekResponsesChatClient(string apiKey)
        : this(new DeepSeekClient(apiKey)) { }

    public DeepSeekResponsesChatClient(HttpClient httpClient)
        : this(new DeepSeekClient(httpClient)) { }

    public DeepSeekResponsesChatClient(HttpClient httpClient, string apiKey)
        : this(new DeepSeekClient(httpClient, apiKey)) { }

    public DeepSeekResponsesChatClient(DeepSeekClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public void Dispose()
    {
        // The HttpClient is owned by the caller or by DeepSeekClient.
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(DeepSeekClient) ? _client : null;
    }

    public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (messages is null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        var request = MapToResponseRequest(messages, options);
        var response = await _client.ResponseAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            var error = new Microsoft.Extensions.AI.ChatResponse(
                [new ChatMessage(ChatRole.Assistant, _client.ErrorMsg)]
            )
            {
                RawRepresentation = _client.ErrorMsg,
            };
            return error;
        }

        return MapToChatResponse(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (messages is null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        var request = MapToResponseRequest(messages, options);
        string? responseId = null;
        string? modelId = null;
        var functionCalls = new Dictionary<string, (string CallId, string Name, string Arguments)>();

        await foreach (
            var responseEvent in _client
                .ResponseStreamAsync(request, cancellationToken)
                .WithCancellation(cancellationToken)
        )
        {
            if (responseEvent.Response is not null)
            {
                responseId = responseEvent.Response.Id;
                modelId = responseEvent.Response.Model;
            }

            if (
                responseEvent.Type == "response.output_item.added"
                && responseEvent.Item?.Type == ResponseInputItemTypes.FunctionCall
            )
            {
                var item = responseEvent.Item;
                var key = item.Id ?? item.CallId ?? Guid.NewGuid().ToString("N");
                functionCalls[key] = (
                    item.CallId ?? string.Empty,
                    item.Name ?? string.Empty,
                    item.Arguments ?? string.Empty
                );
            }
            else if (
                responseEvent.Type == ResponseEventTypes.FunctionCallArgumentsDelta
                && responseEvent.ItemId is not null
                && functionCalls.TryGetValue(responseEvent.ItemId, out var call)
            )
            {
                functionCalls[responseEvent.ItemId] = (
                    call.CallId,
                    call.Name,
                    call.Arguments + (responseEvent.Delta ?? string.Empty)
                );
            }

            var update = MapStreamingEvent(responseEvent, functionCalls);
            if (update is null)
            {
                continue;
            }

            update.ResponseId ??= responseId;
            update.ModelId ??= modelId;
            update.MessageId ??= responseEvent.ItemId;
            update.RawRepresentation = responseEvent;
            update.AdditionalProperties ??= [];
            update.AdditionalProperties["type"] = responseEvent.Type;
            update.AdditionalProperties["sequence_number"] = responseEvent.SequenceNumber;
            yield return update;
        }
    }

    private ResponseRequest MapToResponseRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options
    )
    {
        var inputItems = new List<ResponseInputItem>();
        foreach (var message in messages)
        {
            inputItems.AddRange(MapMessage(message));
        }

        var request = new ResponseRequest
        {
            Model = options?.ModelId ?? DeepSeekModels.Flash,
            Instructions = options?.Instructions,
            MaxOutputTokens = options?.MaxOutputTokens,
            Temperature = options?.Temperature,
            TopP = options?.TopP,
            Text = MapTextOptions(options?.ResponseFormat),
            Input = inputItems.Count == 0
                ? null
                : JsonSerializer.SerializeToNode(inputItems, _client.JsonSerializerOptions),
            Tools = MapTools(options?.Tools),
        };

        if (options?.ToolMode is AutoChatToolMode)
        {
            request.ToolChoice = "auto";
        }
        else if (options?.ToolMode is NoneChatToolMode)
        {
            request.ToolChoice = "none";
        }
        else if (options?.ToolMode is RequiredChatToolMode requiredToolMode)
        {
            request.ToolChoice = string.IsNullOrWhiteSpace(requiredToolMode.RequiredFunctionName)
                ? "required"
                : new JsonObject
                {
                    ["type"] = ResponseToolTypes.Function,
                    ["name"] = requiredToolMode.RequiredFunctionName,
                };
        }

        if (options?.AdditionalProperties is not null)
        {
            if (
                options.AdditionalProperties.TryGetValue("reasoning_effort", out var reasoningEffort)
                && reasoningEffort is string reasoningEffortValue
            )
            {
                request.Reasoning = new ResponseReasoningOptions { Effort = reasoningEffortValue };
            }
            else if (
                options.AdditionalProperties.TryGetValue("reasoning", out var reasoning)
                && reasoning is ResponseReasoningOptions reasoningOptions
            )
            {
                request.Reasoning = reasoningOptions;
            }

            if (
                options.AdditionalProperties.TryGetValue("top_logprobs", out var topLogprobs)
                && topLogprobs is int topLogprobsValue
            )
            {
                request.TopLogprobs = topLogprobsValue;
            }

            if (
                options.AdditionalProperties.TryGetValue("user", out var user)
                && user is string userValue
            )
            {
                request.User = userValue;
            }
        }

        return request;
    }

    private static IEnumerable<ResponseInputItem> MapMessage(ChatMessage message)
    {
        var functionResults = message.Contents.OfType<FunctionResultContent>().ToArray();
        if (message.Role == ChatRole.Tool || functionResults.Length > 0)
        {
            foreach (var functionResult in functionResults)
            {
                yield return ResponseInputItem.NewFunctionCallOutput(
                    functionResult.CallId,
                    SerializeToolResult(functionResult.Result)
                );
            }

            if (functionResults.Length == 0 && !string.IsNullOrWhiteSpace(message.Text))
            {
                yield return ResponseInputItem.NewUserMessage(message.Text);
            }

            yield break;
        }

        foreach (var reasoning in message.Contents.OfType<TextReasoningContent>())
        {
            yield return ResponseInputItem.NewReasoning(
                [
                    new ResponseContentPart
                    {
                        Type = ResponseContentPartTypes.ReasoningText,
                        Text = reasoning.Text,
                    },
                ]
            );
        }

        var functionCalls = message.Contents.OfType<FunctionCallContent>().ToArray();
        if (functionCalls.Length > 0)
        {
            if (!string.IsNullOrWhiteSpace(message.Text))
            {
                yield return ResponseInputItem.NewMessage(message.Role.Value, message.Text);
            }

            foreach (var functionCall in functionCalls)
            {
                yield return ResponseInputItem.NewFunctionCall(
                    functionCall.CallId,
                    functionCall.Name,
                    JsonSerializer.Serialize(functionCall.Arguments ?? new Dictionary<string, object?>())
                );
            }

            yield break;
        }

        yield return ResponseInputItem.NewMessage(message.Role.Value, message.Text);
    }

    private static string SerializeToolResult(object? result)
    {
        return result switch
        {
            null => string.Empty,
            string text => text,
            JsonNode node => node.ToJsonString(),
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(result),
        };
    }

    private static List<ResponseTool>? MapTools(IList<AITool>? tools)
    {
        if (tools is not { Count: > 0 })
        {
            return null;
        }

        var responseTools = new List<ResponseTool>();
        foreach (var tool in tools)
        {
            if (tool is not AIFunctionDeclaration function)
            {
                continue;
            }

            JsonNode? parameters = null;
            try
            {
                parameters = JsonNode.Parse(function.JsonSchema.GetRawText());
            }
            catch (JsonException)
            {
                // Let the service apply its normal validation when a custom schema is invalid.
            }

            responseTools.Add(
                new ResponseTool
                {
                    Type = ResponseToolTypes.Function,
                    Name = function.Name,
                    Description = function.Description,
                    Parameters = parameters,
                }
            );
        }

        return responseTools.Count == 0 ? null : responseTools;
    }

    private static ResponseTextOptions? MapTextOptions(ChatResponseFormat? responseFormat)
    {
        if (responseFormat is null || responseFormat == ChatResponseFormat.Text)
        {
            return null;
        }

        if (responseFormat is ChatResponseFormatJson jsonFormat)
        {
            if (jsonFormat.Schema is { } schema)
            {
                return new ResponseTextOptions
                {
                    Format = new ResponseTextFormat
                    {
                        Type = ResponseTextFormatTypes.JsonSchema,
                        Name = jsonFormat.SchemaName,
                        Schema = JsonNode.Parse(schema.GetRawText()),
                    },
                };
            }

            return new ResponseTextOptions
            {
                Format = new ResponseTextFormat { Type = ResponseTextFormatTypes.JsonObject },
            };
        }

        return null;
    }

    private static Microsoft.Extensions.AI.ChatResponse MapToChatResponse(ResponseResult response)
    {
        var message = new ChatMessage(ChatRole.Assistant, (string?)null);
        foreach (var outputItem in response.Output)
        {
            if (outputItem.Type == ResponseInputItemTypes.Reasoning)
            {
                foreach (var part in outputItem.Content ?? outputItem.Summary ?? [])
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        message.Contents.Add(new TextReasoningContent(part.Text));
                    }
                }
            }
            else if (outputItem.Type == ResponseInputItemTypes.Message)
            {
                foreach (var part in outputItem.Content ?? [])
                {
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        message.Contents.Add(new TextContent(part.Text));
                    }
                }
            }
            else if (outputItem.Type == ResponseInputItemTypes.FunctionCall)
            {
                message.Contents.Add(CreateFunctionCallContent(outputItem));
            }
        }

        var chatResponse = new Microsoft.Extensions.AI.ChatResponse([message])
        {
            ModelId = response.Model,
            ResponseId = response.Id,
            RawRepresentation = response,
            AdditionalProperties = [],
        };

        if (response.CreatedAt > 0)
        {
            chatResponse.CreatedAt = DateTimeOffset.FromUnixTimeSeconds(response.CreatedAt);
        }

        chatResponse.AdditionalProperties["object"] = response.Object;
        chatResponse.AdditionalProperties["status"] = response.Status;
        if (response.Error is not null)
        {
            chatResponse.AdditionalProperties["error"] = response.Error;
        }
        if (response.IncompleteDetails is not null)
        {
            chatResponse.AdditionalProperties["incomplete_details"] = response.IncompleteDetails;
        }

        if (response.Usage is not null)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = response.Usage.InputTokens,
                OutputTokenCount = response.Usage.OutputTokens,
                ReasoningTokenCount = response.Usage.OutputTokensDetails?.ReasoningTokens,
                CachedInputTokenCount = response.Usage.InputTokensDetails?.CachedTokens,
                TotalTokenCount = response.Usage.TotalTokens,
            };
        }

        chatResponse.FinishReason = MapFinishReason(response);
        return chatResponse;
    }

    private static ChatResponseUpdate? MapStreamingEvent(
        ResponseStreamEvent responseEvent,
        IReadOnlyDictionary<string, (string CallId, string Name, string Arguments)> functionCalls
    )
    {
        ChatResponseUpdate? update = responseEvent.Type switch
        {
            ResponseEventTypes.ReasoningTextDelta when !string.IsNullOrEmpty(responseEvent.Delta)
                => new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new TextReasoningContent(responseEvent.Delta)]
                ),
            ResponseEventTypes.OutputTextDelta when !string.IsNullOrEmpty(responseEvent.Delta)
                => new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new TextContent(responseEvent.Delta)]
                ),
            ResponseEventTypes.FunctionCallArgumentsDone
                => MapFunctionCallDone(responseEvent, functionCalls),
            ResponseEventTypes.Completed or ResponseEventTypes.Incomplete or ResponseEventTypes.Failed
                => new ChatResponseUpdate(),
            _ => null,
        };

        if (update is not null && responseEvent.Type is
            ResponseEventTypes.Completed or ResponseEventTypes.Incomplete or ResponseEventTypes.Failed)
        {
            update.FinishReason = MapFinishReason(responseEvent.Response);
        }

        return update;
    }

    private static ChatResponseUpdate? MapFunctionCallDone(
        ResponseStreamEvent responseEvent,
        IReadOnlyDictionary<string, (string CallId, string Name, string Arguments)> functionCalls
    )
    {
        var callId = responseEvent.CallId;
        var name = responseEvent.Name;
        var arguments = responseEvent.Arguments ?? responseEvent.Text ?? string.Empty;
        if (
            responseEvent.ItemId is not null
            && functionCalls.TryGetValue(responseEvent.ItemId, out var call)
        )
        {
            callId = string.IsNullOrEmpty(callId) ? call.CallId : callId;
            name = string.IsNullOrEmpty(name) ? call.Name : name;
            arguments = string.IsNullOrEmpty(arguments) ? call.Arguments : arguments;
        }

        if (string.IsNullOrEmpty(callId) && string.IsNullOrEmpty(name))
        {
            return null;
        }

        var item = new ResponseOutputItem
        {
            Type = ResponseInputItemTypes.FunctionCall,
            CallId = callId,
            Name = name,
            Arguments = arguments,
        };
        return new ChatResponseUpdate(
            ChatRole.Assistant,
            [CreateFunctionCallContent(item)]
        );
    }

    private static FunctionCallContent CreateFunctionCallContent(ResponseOutputItem item)
    {
        IDictionary<string, object?>? arguments = null;
        if (!string.IsNullOrWhiteSpace(item.Arguments))
        {
            try
            {
                arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(item.Arguments);
            }
            catch (JsonException)
            {
                arguments = new Dictionary<string, object?> { ["arguments"] = item.Arguments };
            }
        }

        return new FunctionCallContent(
            item.CallId ?? item.Id ?? string.Empty,
            item.Name ?? string.Empty,
            arguments
        );
    }

    private static ChatFinishReason? MapFinishReason(ResponseResult? response)
    {
        if (response is null)
        {
            return null;
        }

        return response.Status switch
        {
            "completed" => ChatFinishReason.Stop,
            "incomplete" when response.IncompleteDetails?.Reason == "content_filter"
                => ChatFinishReason.ContentFilter,
            "incomplete" => ChatFinishReason.Length,
            _ => null,
        };
    }
}
