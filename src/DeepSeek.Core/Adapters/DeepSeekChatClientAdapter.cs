using System.Runtime.CompilerServices;
using System.Text.Json;
using DeepSeek.Core.Models;
using Microsoft.Extensions.AI;

namespace DeepSeek.Core.Adapters;

/// <summary>
/// Adapter to Microsoft.Extensions.AI IChatClient over DeepSeekClient
/// </summary>
public sealed class DeepSeekChatClientAdapter : IChatClient
{
    private readonly DeepSeekClient _inner;

    public DeepSeekChatClientAdapter(DeepSeekClient inner)
    {
        _inner = inner;
    }

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
        var res = await _inner.ChatAsync(req, cancellationToken).ConfigureAwait(false);
        if (res is null)
        {
            // Return basic response with error text if available
            return new Microsoft.Extensions.AI.ChatResponse(
                [new ChatMessage(ChatRole.Assistant, _inner.ErrorMsg)]
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
        var stream = _inner.ChatStreamAsync(req, cancellationToken);
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
            Messages = new List<Message>(),
            Model = options?.ModelId ?? DeepSeekModels.ChatModel,
            Temperature = options?.Temperature.HasValue == true ? options.Temperature.Value : 1.0,
            TopP = options?.TopP.HasValue == true ? options.TopP.Value : 1.0,
            MaxTokens = options?.MaxOutputTokens ?? 4096,
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

        // Tools mapping (optional): if options.Tools present and contain functions, map to DeepSeek tools
        if (options?.Tools is { Count: > 0 })
        {
            req.Tools = new List<Tool>();
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

        return req;
    }

    private static Microsoft.Extensions.AI.ChatResponse MapToChatResponse(Models.ChatResponse res)
    {
        var content = res.Choices.FirstOrDefault()?.Message?.Content ?? string.Empty;
        var message = new ChatMessage(ChatRole.Assistant, content);
        var chatResponse = new Microsoft.Extensions.AI.ChatResponse([message])
        {
            ModelId = res.Model,
            AdditionalProperties = new AdditionalPropertiesDictionary(),
        };
        chatResponse.AdditionalProperties["id"] = res.Id;
        chatResponse.AdditionalProperties["created"] = res.Created;
        chatResponse.AdditionalProperties["usage_total_tokens"] = res.Usage?.TotalTokens;
        return chatResponse;
    }
}
