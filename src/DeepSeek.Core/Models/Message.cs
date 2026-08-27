using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

[JsonConverter(typeof(MessageJsonConverter))]
public class Message
{
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user";

    /// <summary>
    /// Optional multimodal content blocks. When set, the JSON <c>content</c>
    /// property is serialized as an array instead of a string.
    /// </summary>
    [JsonIgnore]
    public IList<ChatMessageContentPart>? ContentParts { get; set; }

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

    public static Message NewUserMessage(IEnumerable<ChatMessageContentPart> contentParts)
    {
        return NewMessage("user", contentParts);
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

    public static Message NewMessage(
        string role,
        IEnumerable<ChatMessageContentPart> contentParts
    )
    {
        ArgumentNullException.ThrowIfNull(contentParts);
        return new Message
        {
            Role = role,
            ContentParts = contentParts.ToList(),
        };
    }

    /// <summary>
    /// Returns the text blocks in this message. For a legacy string message,
    /// this is equivalent to <see cref="Content"/>.
    /// </summary>
    public string GetTextContent()
    {
        return ContentParts is null
            ? Content
            : string.Concat(
                ContentParts
                    .Where(part => part.Type == ChatMessageContentPartTypes.Text)
                    .Select(part => part.Text ?? string.Empty)
            );
    }
}

/// <summary>
/// Keeps the legacy string <see cref="Message.Content"/> API while accepting
/// the Chat Completions content-block array used by vision requests.
/// </summary>
public sealed class MessageJsonConverter : JsonConverter<Message>
{
    public override Message? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A message must be a JSON object.");
        }

        var message = new Message();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected a message property name.");
            }

            var propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Unexpected end of message JSON.");
            }

            switch (propertyName)
            {
                case "content":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        message.Content = reader.GetString() ?? string.Empty;
                    }
                    else if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        message.ContentParts =
                            JsonSerializer.Deserialize<List<ChatMessageContentPart>>(
                                ref reader,
                                options
                            );
                        message.Content = message.GetTextContent();
                    }
                    else if (reader.TokenType == JsonTokenType.Null)
                    {
                        message.Content = string.Empty;
                    }
                    else
                    {
                        throw new JsonException("Message content must be a string or array.");
                    }
                    break;
                case "role":
                    message.Role = reader.TokenType == JsonTokenType.String
                        ? reader.GetString() ?? string.Empty
                        : string.Empty;
                    break;
                case "name":
                    message.Name = reader.TokenType == JsonTokenType.String
                        ? reader.GetString()
                        : null;
                    break;
                case "prefix":
                    message.Prefix = reader.TokenType == JsonTokenType.Null
                        ? null
                        : reader.GetBoolean();
                    break;
                case "reasoning_content":
                    message.ReasoningContent = reader.TokenType == JsonTokenType.String
                        ? reader.GetString()
                        : null;
                    break;
                case "tool_call_id":
                    message.ToolCallId = reader.TokenType == JsonTokenType.String
                        ? reader.GetString()
                        : null;
                    break;
                case "tool_calls":
                    message.ToolCalls = reader.TokenType == JsonTokenType.Null
                        ? null
                        : JsonSerializer.Deserialize<List<ToolCalls>>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return message;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Message value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WritePropertyName("content");
        if (value.ContentParts is not null)
        {
            writer.WriteStartArray();
            foreach (var part in value.ContentParts)
            {
                JsonSerializer.Serialize(writer, part, options);
            }
            writer.WriteEndArray();
        }
        else
        {
            writer.WriteStringValue(value.Content);
        }

        writer.WriteString("role", value.Role);
        if (value.Name is not null)
        {
            writer.WriteString("name", value.Name);
        }
        if (value.Prefix.HasValue)
        {
            writer.WriteBoolean("prefix", value.Prefix.Value);
        }
        if (value.ReasoningContent is not null)
        {
            writer.WriteString("reasoning_content", value.ReasoningContent);
        }
        if (value.ToolCallId is not null)
        {
            writer.WriteString("tool_call_id", value.ToolCallId);
        }
        if (value.ToolCalls is not null)
        {
            writer.WritePropertyName("tool_calls");
            JsonSerializer.Serialize(writer, value.ToolCalls, options);
        }
        writer.WriteEndObject();
    }
}

public class ToolCalls
{
    public int Index { get; set; }
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
