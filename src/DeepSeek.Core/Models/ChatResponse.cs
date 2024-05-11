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
    public string? Id { get; set; }
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
    public string? Model { get; set; }

    /// <summary>
    /// 对象的类型, 其值为 chat.completion
    /// </summary>
    public string Object { get; set; } = "chat.completion";
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
    public long Index { get; set; }
    public Message? Message { get; set; }
    /// <summary>
    /// 该 choice 的对数概率信息。
    /// </summary>
    public Logprobs? Logprobs { get; set; }

    /// <summary>
    /// 增量返回内容
    /// </summary>
    public Message? Delta { get; set; }
}


/// <summary>
/// 对数概率信息
/// </summary>
public class Logprobs
{
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
    public long CompletionTokens { get; set; }
    [JsonPropertyName("prompt_tokens")]
    public long PromptTokens { get; set; }
    [JsonPropertyName("total_tokens")]
    public long TotalTokens { get; set; }
}

