using DeepSeek.Core.Models;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace DeepSeek.Core.Adapters;

/// <summary>
/// DeepSeekClient for Microsoft.Extensions.AI IChatClient
/// </summary>
public sealed class DeepSeekChatClient : IChatClient
{
    private readonly DeepSeekClient _client;
    private readonly bool _ownsClient;

    public DeepSeekChatClient(string apiKey)
        : this(new DeepSeekClient(apiKey), ownsClient: true) { }

    public DeepSeekChatClient(HttpClient httpClient)
        : this(new DeepSeekClient(httpClient), ownsClient: false) { }

    public DeepSeekChatClient(HttpClient httpClient, string apiKey)
        : this(new DeepSeekClient(httpClient, apiKey), ownsClient: false) { }

    public DeepSeekChatClient(DeepSeekClient client)
        : this(client, ownsClient: false) { }

    private DeepSeekChatClient(DeepSeekClient client, bool ownsClient)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
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
            if (choice.Delta?.ContentParts is { Count: > 0 } contentParts)
            {
                foreach (var part in contentParts)
                {
                    var content = MapContentPart(part);
                    if (content is not null)
                    {
                        yield return new ChatResponseUpdate(ChatRole.Assistant, [content]);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(choice.Delta?.ReasoningContent))
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new TextReasoningContent(choice.Delta.ReasoningContent)]
                );
            }
            else if (!string.IsNullOrWhiteSpace(choice.Delta?.Content))
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new TextContent(choice.Delta.Content)]
                );
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
            Model = options?.ModelId ?? DeepSeekModels.Flash,
            Temperature = options?.Temperature.HasValue == true ? options.Temperature.Value : 1.0,
            TopP = options?.TopP.HasValue == true ? options.TopP.Value : 1.0,
            MaxTokens = options?.MaxOutputTokens ?? 4096,
            ResponseFormat = options?.ResponseFormat == ChatResponseFormat.Json
                ? new ResponseFormat { Type = ResponseFormatTypes.JsonObject }
                : new ResponseFormat { Type = ResponseFormatTypes.Text },
            Stop = options?.StopSequences?.ToList() ?? [],
            Stream = false,
            Logprobs = false, // Default value, could be mapped from custom options
            TopLogprobs = null // Default value, could be mapped from custom options
        };

        foreach (var m in messages)
        {
            req.Messages.Add(MapMessage(m));
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

            if (options.AdditionalProperties.TryGetValue("thinking", out var thinking))
            {
                req.Thinking = thinking switch
                {
                    bool enabled => new Thinking
                    {
                        Type = enabled ? ThinkingTypes.Enabled : ThinkingTypes.Disabled,
                    },
                    string type => new Thinking { Type = type },
                    Thinking thinkingValue => thinkingValue,
                    _ => throw new ArgumentException(
                        "The 'thinking' additional property must be a bool, string, or Thinking instance.",
                        nameof(thinking)
                    ),
                };
            }

            if (options.AdditionalProperties.TryGetValue("reasoning_effort", out var reasoningEffort))
            {
                req.ReasoningEffort = reasoningEffort switch
                {
                    string reasoningEffortValue => reasoningEffortValue,
                    _ => throw new ArgumentException(
                        "The 'reasoning_effort' additional property must be a string.",
                        nameof(reasoningEffort)
                    ),
                };
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

    private static Message MapMessage(ChatMessage message)
    {
        var role = message.Role switch
        {
            var value when value == ChatRole.System => "system",
            var value when value == ChatRole.User => "user",
            var value when value == ChatRole.Assistant => "assistant",
            var value when value == ChatRole.Tool => "tool",
            _ => "user",
        };
        var parts = new List<ChatMessageContentPart>();

        foreach (var content in message.Contents)
        {
            switch (content)
            {
                case TextContent text when !string.IsNullOrEmpty(text.Text):
                    parts.Add(ChatMessageContentPart.CreateTextPart(text.Text));
                    break;
                case UriContent uri when IsImageMediaType(uri.MediaType):
                    parts.Add(
                        ChatMessageContentPart.CreateImageUrlPart(GetUriString(uri.Uri))
                    );
                    break;
                case DataContent data when IsImageMediaType(data.MediaType):
                    parts.Add(ChatMessageContentPart.CreateImageUrlPart(data.Uri));
                    break;
                case HostedFileContent file:
                    parts.Add(ChatMessageContentPart.CreateFilePart(file.FileId));
                    break;
            }
        }

        if (parts.Count == 0)
        {
            return new Message
            {
                Role = role,
                Content = message.Text,
            };
        }

        if (parts.All(part => part.Type == ChatMessageContentPartTypes.Text))
        {
            return new Message
            {
                Role = role,
                Content = string.Concat(parts.Select(part => part.Text)),
            };
        }

        return new Message
        {
            Role = role,
            ContentParts = parts,
        };
    }

    private static bool IsImageMediaType(string? mediaType)
    {
        return mediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string GetUriString(Uri uri)
    {
        return uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString();
    }

    private static AIContent? MapContentPart(ChatMessageContentPart part)
    {
        if (part.Type == ChatMessageContentPartTypes.Text && !string.IsNullOrEmpty(part.Text))
        {
            return new TextContent(part.Text);
        }

        if (
            part.Type == ChatMessageContentPartTypes.ImageUrl
            && !string.IsNullOrWhiteSpace(part.ImageUrl?.Url)
        )
        {
            return MapImageContent(part.ImageUrl.Url);
        }

        if (
            part.Type == ChatMessageContentPartTypes.File
            && !string.IsNullOrWhiteSpace(part.FileId)
        )
        {
            return new HostedFileContent(part.FileId)
            {
                Name = part.Filename,
                MediaType = "image/*",
            };
        }

        if (
            part.Type == ChatMessageContentPartTypes.File
            && !string.IsNullOrWhiteSpace(part.FileData)
        )
        {
            return MapDataContent(part.FileData, part.Filename);
        }

        return null;
    }

    private static AIContent? MapImageContent(string imageUrl)
    {
        return imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            ? MapDataContent(imageUrl, null)
            : new UriContent(imageUrl, "image/*");
    }

    private static DataContent? MapDataContent(string dataUri, string? name)
    {
        var commaIndex = dataUri.IndexOf(',');
        if (
            !dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || commaIndex <= "data:".Length
        )
        {
            return null;
        }

        var metadata = dataUri["data:".Length..commaIndex];
        var mediaType = metadata.Split(';', 2)[0];
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            mediaType = "application/octet-stream";
        }

        try
        {
            return new DataContent(dataUri, mediaType) { Name = name };
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Microsoft.Extensions.AI.ChatResponse MapToChatResponse(Models.ChatResponse res)
    {
        var sourceMessage = res.Choices.FirstOrDefault()?.Message;
        var message = new ChatMessage(ChatRole.Assistant, (string?)null);
        if (sourceMessage?.ContentParts is { Count: > 0 } contentParts)
        {
            foreach (var part in contentParts)
            {
                var content = MapContentPart(part);
                if (content is not null)
                {
                    message.Contents.Add(content);
                }
            }
        }
        else if (!string.IsNullOrEmpty(sourceMessage?.Content))
        {
            message.Contents.Add(new TextContent(sourceMessage.Content));
        }

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
            RawRepresentation = res,
            AdditionalProperties = [],
        };

        if (sourceMessage?.ContentParts is { Count: > 0 })
        {
            chatResponse.AdditionalProperties["content_parts"] = sourceMessage.ContentParts;
        }

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
