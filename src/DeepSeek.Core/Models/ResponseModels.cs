using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

/// <summary>
/// Request for the OpenAI-compatible DeepSeek Responses API.
/// </summary>
public class ResponseRequest
{
    /// <summary>
    /// The model to use.
    /// </summary>
    public string Model { get; set; } = DeepSeekModels.Flash;

    /// <summary>
    /// A string input or an array of Responses input items.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Input { get; set; }

    /// <summary>
    /// System-level instructions sent as the first system message.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Instructions { get; set; }

    /// <summary>
    /// Reasoning configuration.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseReasoningOptions? Reasoning { get; set; }

    /// <summary>
    /// Maximum number of generated tokens, including reasoning tokens.
    /// </summary>
    [JsonPropertyName("max_output_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Whether to return semantic SSE events.
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// Sampling temperature.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    /// <summary>
    /// Nucleus sampling value.
    /// </summary>
    [JsonPropertyName("top_p")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? TopP { get; set; }

    /// <summary>
    /// Text output configuration.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseTextOptions? Text { get; set; }

    /// <summary>
    /// Tools available to the model.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ResponseTool>? Tools { get; set; }

    /// <summary>
    /// Controls how tools are selected. It can be a string or an object.
    /// </summary>
    [JsonPropertyName("tool_choice")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? ToolChoice { get; set; }

    /// <summary>
    /// Number of top log probabilities to return.
    /// </summary>
    [JsonPropertyName("top_logprobs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TopLogprobs { get; set; }

    /// <summary>
    /// Optional end-user identifier used for rate-limit and cache isolation.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? User { get; set; }
}

/// <summary>
/// Reasoning configuration for a Responses request.
/// </summary>
public class ResponseReasoningOptions
{
    /// <summary>
    /// Reasoning effort such as none, low, high, or max.
    /// </summary>
    public string? Effort { get; set; }

    /// <summary>
    /// Optional summary configuration. DeepSeek currently accepts but does not generate summaries.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Summary { get; set; }
}

/// <summary>
/// Text configuration for a Responses request.
/// </summary>
public class ResponseTextOptions
{
    /// <summary>
    /// Requested output format.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseTextFormat? Format { get; set; }

    /// <summary>
    /// Optional verbosity setting accepted by the OpenAI schema.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Verbosity { get; set; }
}

/// <summary>
/// Text output format for a Responses request.
/// </summary>
public class ResponseTextFormat
{
    public string Type { get; set; } = ResponseTextFormatTypes.Text;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Schema { get; set; }
}

/// <summary>
/// A tool definition in Responses format.
/// </summary>
public class ResponseTool
{
    public string Type { get; set; } = ResponseToolTypes.Function;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Parameters { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Strict { get; set; }
}

/// <summary>
/// An input item accepted by the Responses API.
/// </summary>
public class ResponseInputItem
{
    public string Type { get; set; } = ResponseInputItemTypes.Message;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Content { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CallId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Arguments { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Output { get; set; }

    public static ResponseInputItem NewMessage(string role, string content)
    {
        return new ResponseInputItem
        {
            Type = ResponseInputItemTypes.Message,
            Role = role,
            Content = content,
        };
    }

    public static ResponseInputItem NewUserMessage(string content) => NewMessage("user", content);

    public static ResponseInputItem NewSystemMessage(string content) => NewMessage("system", content);

    public static ResponseInputItem NewAssistantMessage(string content) => NewMessage("assistant", content);

    public static ResponseInputItem NewFunctionCall(string callId, string name, string arguments)
    {
        return new ResponseInputItem
        {
            Type = ResponseInputItemTypes.FunctionCall,
            CallId = callId,
            Name = name,
            Arguments = arguments,
        };
    }

    public static ResponseInputItem NewFunctionCallOutput(string callId, string output)
    {
        return new ResponseInputItem
        {
            Type = ResponseInputItemTypes.FunctionCallOutput,
            CallId = callId,
            Output = output,
        };
    }

    public static ResponseInputItem NewReasoning(IEnumerable<ResponseContentPart> content)
    {
        return new ResponseInputItem
        {
            Type = ResponseInputItemTypes.Reasoning,
            Content = System.Text.Json.JsonSerializer.SerializeToNode(
                content,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            ),
        };
    }
}

/// <summary>
/// A content part in an input or output item.
/// </summary>
public class ResponseContentPart
{
    public string Type { get; set; } = ResponseContentPartTypes.OutputText;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Annotations { get; set; }
}

/// <summary>
/// A non-streaming Responses API result.
/// </summary>
public class ResponseResult
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    public string Status { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseError? Error { get; set; }

    [JsonPropertyName("incomplete_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseIncompleteDetails? IncompleteDetails { get; set; }

    public string Model { get; set; } = string.Empty;
    public List<ResponseOutputItem> Output { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseUsage? Usage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Store { get; set; }

    [JsonPropertyName("previous_response_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousResponseId { get; set; }

    [JsonPropertyName("parallel_tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? ParallelToolCalls { get; set; }

    /// <summary>
    /// Concatenated text from all assistant message output items.
    /// </summary>
    [JsonIgnore]
    public string OutputText => GetOutputText();

    public string GetOutputText()
    {
        return string.Concat(
            (Output ?? [])
                .Where(item => item.Type == ResponseInputItemTypes.Message)
                .SelectMany(item => item.Content ?? [])
                .Where(part => part.Type == ResponseContentPartTypes.OutputText)
                .Select(part => part.Text ?? string.Empty)
        );
    }
}

/// <summary>
/// A Responses output item. The fields are intentionally optional because the API uses
/// one shape for message, reasoning, function_call, and web_search_call items.
/// </summary>
public class ResponseOutputItem
{
    public string Type { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Status { get; set; }
    public string? Role { get; set; }
    public List<ResponseContentPart>? Content { get; set; }
    public List<ResponseContentPart>? Summary { get; set; }

    [JsonPropertyName("call_id")]
    public string? CallId { get; set; }

    public string? Name { get; set; }
    public string? Arguments { get; set; }
    public JsonNode? Action { get; set; }
}

public class ResponseError
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public class ResponseIncompleteDetails
{
    public string? Reason { get; set; }
}

public class ResponseUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public ResponseInputTokenDetails? InputTokensDetails { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("output_tokens_details")]
    public ResponseOutputTokenDetails? OutputTokensDetails { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

public class ResponseInputTokenDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}

public class ResponseOutputTokenDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

/// <summary>
/// A semantic SSE event emitted by the Responses API.
/// </summary>
public class ResponseStreamEvent
{
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("sequence_number")]
    public int SequenceNumber { get; set; }

    [JsonPropertyName("response")]
    public ResponseResult? Response { get; set; }

    [JsonPropertyName("output_index")]
    public int? OutputIndex { get; set; }

    public ResponseOutputItem? Item { get; set; }
    public ResponseContentPart? Part { get; set; }

    [JsonPropertyName("item_id")]
    public string? ItemId { get; set; }

    [JsonPropertyName("content_index")]
    public int? ContentIndex { get; set; }

    [JsonPropertyName("call_id")]
    public string? CallId { get; set; }

    public string? Name { get; set; }
    public string? Delta { get; set; }
    public string? Text { get; set; }
    public string? Arguments { get; set; }
    public ResponseError? Error { get; set; }

    /// <summary>
    /// The SSE event name, when it was present on the wire.
    /// </summary>
    [JsonIgnore]
    public string? EventName { get; internal set; }
}
