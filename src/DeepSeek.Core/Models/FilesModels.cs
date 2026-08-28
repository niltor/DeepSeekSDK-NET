using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

/// <summary>
/// Supported Files API purposes.
/// </summary>
public static class FilePurposes
{
    public const string UserData = "user_data";
}

/// <summary>
/// File list ordering values.
/// </summary>
public static class FileListOrderTypes
{
    public const string Ascending = "asc";
    public const string Descending = "desc";
}

/// <summary>
/// Options used when uploading a file through the OpenAI-compatible Files API.
/// </summary>
public class FileUploadOptions
{
    /// <summary>
    /// The Files API purpose. DeepSeek currently accepts only <c>user_data</c>.
    /// </summary>
    public string Purpose { get; set; } = FilePurposes.UserData;

    /// <summary>
    /// Optional MIME type for the multipart file part. DeepSeek detects the image
    /// format from the file bytes.
    /// </summary>
    [JsonIgnore]
    public string? ContentType { get; set; }

    /// <summary>
    /// Optional lifetime in seconds. DeepSeek accepts values from one hour to 30 days.
    /// </summary>
    [JsonIgnore]
    public int? ExpiresAfterSeconds { get; set; }
}

/// <summary>
/// Query options for listing Files API files.
/// </summary>
public class FileListOptions
{
    public string? After { get; set; }
    public int? Limit { get; set; }
    public string? Order { get; set; }
    /// <summary>
    /// Optional purpose filter. DeepSeek currently accepts only <c>user_data</c>.
    /// </summary>
    public string? Purpose { get; set; }
}

/// <summary>
/// A file stored by DeepSeek Files API.
/// </summary>
public class FileObject
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public long Bytes { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    public string Filename { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("expires_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ExpiresAt { get; set; }
}

/// <summary>
/// Cursor-paginated response from GET /files.
/// </summary>
public class FileListResponse
{
    public string Object { get; set; } = string.Empty;
    public List<FileObject> Data { get; set; } = [];

    [JsonPropertyName("first_id")]
    public string? FirstId { get; set; }

    [JsonPropertyName("last_id")]
    public string? LastId { get; set; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

/// <summary>
/// Response from DELETE /files/{file_id}.
/// </summary>
public class FileDeletionResponse
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;
    public bool Deleted { get; set; }
}

/// <summary>
/// Alias matching the verb used by the DELETE endpoint.
/// </summary>
public class FileDeleteResponse : FileDeletionResponse
{
}
