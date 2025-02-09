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

    public static Message NewUserMessage(string content)
    {
        return new Message
        {
            Content = content,
            Role = "user"
        };
    }

    public static Message NewSystemMessage(string content)
    {
        return new Message
        {
            Content = content,
            Role = "system"
        };
    }

    public static Message NewAssistantMessage(string content, bool? prefix = false, string? reasoningContent = null)
    {
        return new Message
        {
            Content = content,
            Role = "assistant",
            Prefix = prefix,
            ReasoningContent = reasoningContent
        };
    }

    public static Message NewToolMessage(string content, string toolCallId)
    {
        return new Message
        {
            Content = content,
            Role = "tool",
            ToolCallId = toolCallId
        };
    }
}
