using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

/// <summary>
/// Request sent to DeepSeek's standalone Search API.
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// Search query. DeepSeek accepts 1 to 128 Unicode characters.
    /// </summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of results to return. DeepSeek accepts 1 to 50 and
    /// defaults to 10 when omitted.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Count { get; set; }

    /// <summary>
    /// The original user question that caused the search, when available.
    /// </summary>
    [JsonPropertyName("ori_question")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OriginalQuestion { get; set; }

    /// <summary>
    /// Optional IP address used to localize search results.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Localization { get; set; }

    /// <summary>
    /// Whether the response should include extracted page content.
    /// </summary>
    [JsonPropertyName("need_content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? NeedContent { get; set; }
}

/// <summary>
/// Response returned by DeepSeek's standalone Search API.
/// </summary>
public class SearchResponse
{
    [JsonRequired]
    public List<SearchResult> Results { get; set; } = [];
}

/// <summary>
/// A ranked result returned by DeepSeek Search.
/// </summary>
public class SearchResult
{
    [JsonRequired]
    public double RerankScore { get; set; }
    [JsonRequired]
    public string Title { get; set; } = string.Empty;
    [JsonRequired]
    public string Url { get; set; } = string.Empty;
    [JsonRequired]
    public string Snippet { get; set; } = string.Empty;
    [JsonRequired]
    public string Summary { get; set; } = string.Empty;
    [JsonRequired]
    public string Content { get; set; } = string.Empty;
    [JsonRequired]
    public string? SiteName { get; set; }
    [JsonRequired]
    public string? SiteIcon { get; set; }
    [JsonRequired]
    public DateTimeOffset? Date { get; set; }
    [JsonRequired]
    public int Authority { get; set; }
}

/// <summary>
/// Request sent to DeepSeek's standalone Fetch API.
/// </summary>
public class FetchRequest
{
    /// <summary>
    /// HTTP or HTTPS page URL to fetch. URLs may be at most 8192 characters
    /// and may not contain user information.
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Response returned by DeepSeek's standalone Fetch API.
/// </summary>
public class FetchResponse
{
    [JsonRequired]
    public string Title { get; set; } = string.Empty;
    [JsonRequired]
    public string Content { get; set; } = string.Empty;
}
