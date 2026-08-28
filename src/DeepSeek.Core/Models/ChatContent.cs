using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

/// <summary>
/// Content block types accepted by the Chat Completions API.
/// </summary>
public static class ChatMessageContentPartTypes
{
    public const string Text = "text";
    public const string ImageUrl = "image_url";
    public const string File = "file";
}

/// <summary>
/// Detail values accepted for image inputs.
/// </summary>
public static class ImageDetailTypes
{
    public const string Low = "low";
    public const string High = "high";
    public const string Original = "original";
    public const string Auto = "auto";
}

/// <summary>
/// A content block in a Chat Completions message.
///
/// Text messages remain available through <see cref="Message.Content"/>. Use
/// <see cref="Message.ContentParts"/> when a message contains an image or file.
/// </summary>
public class ChatMessageContentPart
{
    public string Type { get; set; } = ChatMessageContentPartTypes.Text;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ChatImageUrl? ImageUrl { get; set; }

    [JsonPropertyName("file_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileId { get; set; }

    [JsonPropertyName("file_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileData { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }

    public static ChatMessageContentPart CreateTextPart(string text)
    {
        return new ChatMessageContentPart
        {
            Type = ChatMessageContentPartTypes.Text,
            Text = text,
        };
    }

    public static ChatMessageContentPart CreateImageUrlPart(
        string imageUrl,
        string? detail = null
    )
    {
        return new ChatMessageContentPart
        {
            Type = ChatMessageContentPartTypes.ImageUrl,
            ImageUrl = new ChatImageUrl
            {
                Url = imageUrl,
                Detail = detail,
            },
        };
    }

    public static ChatMessageContentPart CreateImageDataPart(
        ReadOnlyMemory<byte> data,
        string mediaType,
        string? detail = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return CreateImageUrlPart(
            $"data:{mediaType};base64,{Convert.ToBase64String(data.Span)}",
            detail
        );
    }

    public static ChatMessageContentPart CreateFilePart(string fileId)
    {
        return new ChatMessageContentPart
        {
            Type = ChatMessageContentPartTypes.File,
            FileId = fileId,
        };
    }

    public static ChatMessageContentPart CreateFileDataPart(
        string fileData,
        string? filename = null
    )
    {
        return new ChatMessageContentPart
        {
            Type = ChatMessageContentPartTypes.File,
            FileData = fileData,
            Filename = filename,
        };
    }

    public static ChatMessageContentPart CreateFileDataPart(
        ReadOnlyMemory<byte> data,
        string mediaType,
        string? filename = null
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        return CreateFileDataPart(
            $"data:{mediaType};base64,{Convert.ToBase64String(data.Span)}",
            filename
        );
    }
}

/// <summary>
/// The nested payload used by an <c>image_url</c> Chat Completions content block.
/// </summary>
public class ChatImageUrl
{
    public string Url { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }
}
