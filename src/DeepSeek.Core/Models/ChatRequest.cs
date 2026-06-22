using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;

/// <summary>
/// chat请求
/// </summary>
public class ChatRequest
{
    private Thinking? _thinking;
    private string? _reasoningEffort;
    private bool _thinkingSet;
    private bool _reasoningEffortSet;

    /// <summary>
    /// 消息列表
    /// </summary>
    public List<Message> Messages { get; set; } = [];

    /// <summary>
    /// 使用的模型的 ID。您可以使用 deepseek-v4-pro 或者 deepseek-v4-flash。
    /// </summary>
    public string Model { get; set; } = DeepSeekModels.Flash;

    /// <summary>
    /// 控制 thinking 和 non-thinking 模式之间的开关。
    /// </summary>
    [JsonIgnore]
    public Thinking Thinking
    {
        get => _thinking ?? new Thinking();
        set
        {
            _thinking = value;
            _thinkingSet = value is not null;
        }
    }

    /// <summary>
    /// 控制模型的推理强度。
    /// </summary>
    [JsonIgnore]
    public string ReasoningEffort
    {
        get => _reasoningEffort ?? ReasoningEffortTypes.High;
        set
        {
            _reasoningEffort = value;
            _reasoningEffortSet = value is not null;
        }
    }

    [JsonPropertyName("thinking")]
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal Thinking? ThinkingValue
    {
        get => _thinkingSet ? _thinking ?? new Thinking() : null;
        set
        {
            _thinking = value;
            _thinkingSet = value is not null;
        }
    }

    [JsonPropertyName("reasoning_effort")]
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal string? ReasoningEffortValue
    {
        get => _reasoningEffortSet ? _reasoningEffort ?? ReasoningEffortTypes.High : null;
        set
        {
            _reasoningEffort = value;
            _reasoningEffortSet = value is not null;
        }
    }

    /// <summary>
    /// 限制一次请求中模型生成 completion 的最大 token 数。输入 token 和输出 token 的总长度受模型的上下文长度的限制。
    /// default:4096
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public long MaxTokens { get; set; } = 4096;

    /// <summary>
    /// type:text or json_object
    /// </summary>
    [JsonPropertyName("response_format")]
    public ResponseFormat? ResponseFormat { get; set; }

    /// <summary>
    /// Up to 16 sequences where the API will stop generating further tokens.
    /// </summary>
    public List<string> Stop { get; set; } = [];

    /// <summary>
    /// 如果设置为 True，将会以 SSE（server-sent events）的形式以流式发送消息增量。消息流以 data: [DONE] 结尾。
    /// </summary>
    [JsonInclude]
    public bool Stream { get; set; }

    /// <summary>
    /// max 128 functions
    /// </summary>
    public List<Tool>? Tools { get; set; }

    /// <summary>
    /// tool choice
    /// </summary>
    [JsonPropertyName("tool_choice")]
    public JsonElement? ToolChoice { get; set; }

    [JsonPropertyName("stream_options")]
    public StreamOptions? StreamOptions { get; set; }

    /// <summary>
    /// 采样温度，介于 0 和 2 之间。更高的值，如 0.8，会使输出更随机，而更低的值，如 0.2，会使其更加集中和确定。 我们通常建议可以更改这个值或者更改 top_p，但不建议同时对两者进行修改。
    public double Temperature { get; set; } = 1;

    /// <summary>
    /// 作为调节采样温度的替代方案，模型会考虑前 top_p 概率的 token 的结果。所以 0.1 就意味着只有包括在最高 10% 概率中的 token 会被考虑。 我们通常建议修改这个值或者更改 temperature，但不建议同时对两者进行修改。
    /// </summary>
    [JsonPropertyName("top_p")]
    public double TopP { get; set; } = 1;

    /// <summary>
    /// 是否返回所输出 token 的对数概率。如果为 true，则在 message 的 content 中返回每个输出 token 的对数概率
    /// </summary>
    [JsonPropertyName("logprobs")]
    public bool Logprobs { get; set; }

    /// <summary>
    /// 一个介于 0 到 20 之间的整数 N，指定每个输出位置返回输出概率 top N 的 token，且返回这些 token 的对数概率。指定此参数时，logprobs 必须为 true。
    /// </summary>
    [JsonPropertyName("top_logprobs")]
    public int? TopLogprobs { get; set; }
}

public class StreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
}

public class Thinking
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = ThinkingTypes.Enabled;
}

public class Tool
{
    public string Type { get; set; } = "function";
    public RequestFunction Function { get; set; } = default!;
}

public class RequestFunction
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public JsonNode Parameters { get; set; } =
        new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() };
}

public class ResponseFormat
{
    public string Type { get; set; } = ResponseFormatTypes.Text;
}
