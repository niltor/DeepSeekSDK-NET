using DeepSeek.Core.Models;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DeepSeek.Core.Adapters;

/// <summary>
/// DeepSeekClient for Microsoft.Extensions.AI IChatClient
/// </summary>
public sealed class DeepSeekChatClient(string apiKey) : IChatClient
{
    private readonly DeepSeekClient _client = new(apiKey);

    public void Dispose()
    {
        // underlying HttpClient lifetime is managed by DeepSeekClient owner
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        // No additional services exposed for now
        return null;
    }

    public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (messages is null)
            throw new ArgumentNullException(nameof(messages));
        var req = MapToChatRequest(messages, options);
        var res = await _client.ChatAsync(req, cancellationToken).ConfigureAwait(false);
        if (res is null)
        {
            // Return basic response with error text if available
            return new Microsoft.Extensions.AI.ChatResponse(
                [new ChatMessage(ChatRole.Assistant, _client.ErrorMsg)]
            );
        }
        return MapToChatResponse(res);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        if (messages is null)
            throw new ArgumentNullException(nameof(messages));
        var req = MapToChatRequest(messages, options);
        req.Stream = true; // Enable streaming for this request
        var stream = _client.ChatStreamAsync(req, cancellationToken);
        if (stream is null)
        {
            yield break;
        }
        await foreach (Choice choice in stream.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(choice.Delta?.ReasoningContent))
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    choice.Delta.ReasoningContent
                );
            }
            if (!string.IsNullOrWhiteSpace(choice.Delta?.Content))
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, choice.Delta.Content);
            }
        }
    }

    private static ChatRequest MapToChatRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options
    )
    {
        var req = new ChatRequest
        {
            Messages = [],
            Model = options?.ModelId ?? DeepSeekModels.ChatModel,
            Temperature = options?.Temperature.HasValue == true ? options.Temperature.Value : 1.0,
            TopP = options?.TopP.HasValue == true ? options.TopP.Value : 1.0,
            MaxTokens = options?.MaxOutputTokens ?? 4096,
            ResponseFormat = options?.ResponseFormat == ChatResponseFormat.Json
                ? new ResponseFormat { Type = ResponseFormatTypes.JsonObject }
                : new ResponseFormat { Type = ResponseFormatTypes.Text },
            FrequencyPenalty = options?.FrequencyPenalty ?? 0.0,
            PresencePenalty = options?.PresencePenalty ?? 0.0,
            Stop = options?.StopSequences?.ToList() ?? [],
            Stream = false,
            Logprobs = false, // Default value, could be mapped from custom options
            TopLogprobs = null // Default value, could be mapped from custom options
        };

        foreach (var m in messages)
        {
            var role = m.Role;
            if (role == ChatRole.System)
            {
                req.Messages.Add(Message.NewSystemMessage(m.Text));
            }
            else if (role == ChatRole.User)
            {
                req.Messages.Add(Message.NewUserMessage(m.Text));
            }
            else if (role == ChatRole.Assistant)
            {
                req.Messages.Add(Message.NewAssistantMessage(m.Text));
            }
            else if (role == ChatRole.Tool)
            {
                // Tool messages need a tool_call_id; not available in abstractions without tool flow
                req.Messages.Add(new Message { Role = "tool", Content = m.Text });
            }
            else
            {
                req.Messages.Add(Message.NewUserMessage(m.Text));
            }
        }

        if (options?.Tools is { Count: > 0 })
        {
            req.Tools = [];
            foreach (var tool in options.Tools)
            {
                if (tool is DelegatingAIFunction dfn)
                {
                    System.Text.Json.Nodes.JsonNode? schemaNode = null;
                    try
                    {
                        var json = dfn.JsonSchema.GetRawText();
                        schemaNode = System.Text.Json.Nodes.JsonNode.Parse(json);
                    }
                    catch { }

                    req.Tools.Add(
                        new Tool
                        {
                            Function = new RequestFunction
                            {
                                Name = dfn.Name,
                                Description = dfn.Description,
                                Parameters = schemaNode ?? new System.Text.Json.Nodes.JsonObject(),
                            },
                        }
                    );
                }
                else if (tool is AIFunction fn)
                {
                    // Fallback without schema
                    req.Tools.Add(
                        new Tool
                        {
                            Function = new RequestFunction
                            {
                                Name = fn.Name,
                                Description = fn.Description ?? string.Empty,
                                Parameters = new System.Text.Json.Nodes.JsonObject(),
                            },
                        }
                    );
                }
            }
        }

        // Tool mode mapping - common modes
        if (options?.ToolMode is AutoChatToolMode)
        {
            req.ToolChoice = JsonDocument.Parse("\"auto\"").RootElement;
        }
        else if (options?.ToolMode is RequiredChatToolMode)
        {
            req.ToolChoice = JsonDocument.Parse("\"required\"").RootElement;
        }
        else if (options?.ToolMode is NoneChatToolMode)
        {
            req.ToolChoice = JsonDocument.Parse("\"none\"").RootElement;
        }

        if (options?.AdditionalProperties != null)
        {
            if (options.AdditionalProperties.TryGetValue("include_usage", out var includeUsage) && includeUsage is bool usageValue)
            {
                req.StreamOptions = new StreamOptions { IncludeUsage = usageValue };
            }

            if (options.AdditionalProperties.TryGetValue("logprobs", out var logprobs) && logprobs is bool logprobsValue)
            {
                req.Logprobs = logprobsValue;
            }

            if (options.AdditionalProperties.TryGetValue("top_logprobs", out var topLogprobs) && topLogprobs is int topLogprobsValue)
            {
                req.TopLogprobs = topLogprobsValue;
            }
        }

        return req;
    }

    private static Microsoft.Extensions.AI.ChatResponse MapToChatResponse(Models.ChatResponse res)
    {
        var content = res.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var message = new ChatMessage(ChatRole.Assistant, content);

        // Handle tool calls if present in the first choice
        var firstChoice = res.Choices.FirstOrDefault();
        if (firstChoice?.Message?.ToolCalls?.Any() == true)
        {
            foreach (var toolCall in firstChoice.Message.ToolCalls)
            {
                if (toolCall.Function != null)
                {
                    // Parse arguments as dictionary if it's a JSON string
                    IDictionary<string, object?>? argsDict = null;
                    if (!string.IsNullOrEmpty(toolCall.Function.Arguments))
                    {
                        try
                        {
                            argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.Function.Arguments);
                        }
                        catch
                        {
                            // If parsing fails, create a simple dictionary with the raw string
                            argsDict = new Dictionary<string, object?> { ["arguments"] = toolCall.Function.Arguments };
                        }
                    }

                    var functionCall = new FunctionCallContent(
                        toolCall.Id ?? string.Empty,
                        toolCall.Function.Name ?? string.Empty,
                        argsDict
                    );

                    message.Contents.Add(functionCall);
                }
            }
        }

        var chatResponse = new Microsoft.Extensions.AI.ChatResponse([message])
        {
            ModelId = res.Model,
            AdditionalProperties = [],
        };

        // Add comprehensive metadata
        chatResponse.AdditionalProperties["id"] = res.Id;
        chatResponse.AdditionalProperties["created"] = res.Created;
        if (!string.IsNullOrEmpty(res.SystemFingerprint))
        {
            chatResponse.AdditionalProperties["system_fingerprint"] = res.SystemFingerprint;
        }
        chatResponse.AdditionalProperties["object"] = res.Object;

        // Add usage information if available
        if (res.Usage != null)
        {
            chatResponse.AdditionalProperties["usage_total_tokens"] = res.Usage.TotalTokens;
            chatResponse.AdditionalProperties["usage_prompt_tokens"] = res.Usage.PromptTokens;
            chatResponse.AdditionalProperties["usage_completion_tokens"] = res.Usage.CompletionTokens;

            // Set the standard usage property if Microsoft.Extensions.AI supports it
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = res.Usage.PromptTokens,
                OutputTokenCount = res.Usage.CompletionTokens,
                TotalTokenCount = res.Usage.TotalTokens
            };
        }

        // Add finish reason from first choice
        if (firstChoice != null && !string.IsNullOrEmpty(firstChoice.FinishReason))
        {
            ChatFinishReason? finishReason = firstChoice.FinishReason switch
            {
                "stop" => ChatFinishReason.Stop,
                "length" => ChatFinishReason.Length,
                "tool_calls" => ChatFinishReason.ToolCalls,
                "content_filter" => ChatFinishReason.ContentFilter,
                _ => null
            };

            if (finishReason.HasValue)
            {
                chatResponse.AdditionalProperties["finish_reason"] = finishReason.Value;
            }
        }

        return chatResponse;
    }
}
