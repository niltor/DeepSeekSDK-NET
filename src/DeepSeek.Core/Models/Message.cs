using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

public class Message
{
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user";

    public string? Name { get; set; }

    /// <summary>
    /// beta feature
    /// </summary>
    public bool? Prefix { get; set; }

    /// <summary>
    /// beta feature
    /// </summary>
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }

    [JsonPropertyName("tool_call_id")]
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Tool calls made by the assistant.
    /// </summary>
    public List<ToolCalls>? ToolCalls { get; set; }

    public static Message NewUserMessage(string content)
    {
        return new Message { Content = content, Role = "user" };
    }

    public static Message NewSystemMessage(string content)
    {
        return new Message { Content = content, Role = "system" };
    }

    public static Message NewAssistantMessage(
        string content,
        bool? prefix = false,
        string? reasoningContent = null
    )
    {
        return new Message
        {
            Content = content,
            Role = "assistant",
            Prefix = prefix,
            ReasoningContent = reasoningContent,
        };
    }

    public static Message NewToolMessage(string content, string toolCallId)
    {
        return new Message
        {
            Content = content,
            Role = "tool",
            ToolCallId = toolCallId,
        };
    }
}

public class ToolCalls
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public ToolCallsFunction Function { get; set; } = default!;

    public class ToolCallsFunction
    {
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = string.Empty;
    }
}
