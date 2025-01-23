using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;
/// <summary>
/// chat response
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// 该对话的唯一标识符。
    /// </summary>
    public string Id { get; set; } = default!;
    /// <summary>
    /// 模型生成的 completion 的选择列表
    /// </summary>
    public List<Choice> Choices { get; set; } = [];
    /// <summary>
    /// 创建聊天完成时的 Unix 时间戳（以秒为单位）。
    /// </summary>

    public long Created { get; set; }
    /// <summary>
    /// 生成该 completion 的模型名
    /// </summary>
    public string Model { get; set; } = default!;

    [JsonPropertyName("system_fingerprint")]
    public string? SystemFingerprint { get; set; }

    /// <summary>
    /// 对象的类型, 其值为 chat.completion
    /// </summary>
    public string Object { get; set; } = default!;
    /// <summary>
    /// 该对话补全请求的用量信息
    /// </summary>
    public Usage? Usage { get; set; }
}

/// <summary>
/// 模型生成的选择
/// </summary>
public class Choice
{
    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    public int Index { get; set; }
    public Message? Message { get; set; }
    /// <summary>
    /// 该 choice 的对数概率信息。
    /// </summary>
    public Logprobs? Logprobs { get; set; }

    /// <summary>
    /// use this when streaming 
    /// </summary>
    public Message? Delta { get; set; }

    /// <summary>
    /// for completion 
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 对数概率信息
/// </summary>
public class Logprobs
{
    [JsonPropertyName("text_offset")]
    public int[] TextOffset { get; set; } = [];

    public string[] Tokens { get; set; } = [];
    [JsonPropertyName("token_logprobs")]
    public double[] TokenLogProbs { get; set; } = [];

    [JsonPropertyName("top_logprobs")]
    public List<TopLogprobs>? TopLogProbs { get; set; }

    /// <summary>
    /// 包含输出 token 对数概率信息的列表
    /// </summary>
    public List<Content> Content { get; set; } = [];
}

/// <summary>
/// 对数概率信息
/// </summary>
public class Content
{
    public string? Token { get; set; }
    public long Logprob { get; set; }
    public byte[] Bytes { get; set; } = [];
    [JsonPropertyName("top_logprobs")]
    public List<TopLogprobs> TopLogprobs { get; set; } = [];
}

public class TopLogprobs
{
    public string? Token { get; set; }
    public long Logprob { get; set; }
    public byte[] Bytes { get; set; } = [];
}

/// <summary>
/// 用量信息
/// </summary>
public class Usage
{
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("prompt_cache_hit_tokens")]
    public int PromptCacheHitTokens { get; set; }
    [JsonPropertyName("prompt_cache_miss_tokens")]
    public int PromptCacheMissTokens { get; set; }
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("prompt_tokens_details")]
    public CompletionTokensDetails Details { get; set; }

    public class CompletionTokensDetails
    {
        [JsonPropertyName("reasoning_tokens")]
        public int ReasoningTokens { get; set; }
        [JsonPropertyName("cached_tokens")]
        public int CachedTokens { get; set; }
    }
}


