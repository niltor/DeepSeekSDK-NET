using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace DeepSeek.Core.Models;
public class CompletionRequest
{
    /// <summary>
    /// prompt
    /// </summary>
    public required string Prompt { get; set; }
    /// <summary>
    /// 使用的模型的 ID。您可以使用 deepseek-chat 或者 deepseek-reasoner。
    /// </summary>
    public string Model { get; set; } = DeepSeekModels.ChatModel;

    /// <summary>
    /// output prompt
    /// </summary>
    public bool? Echo { get; set; }

    /// <summary>
    /// 介于 -2.0 和 2.0 之间的数字。如果该值为正，那么新 token 会根据其在已有文本中的出现频率受到相应的惩罚，降低模型重复相同内容的可能性。
    /// </summary>
    [JsonPropertyName("frequency_penalty")]
    public double FrequencyPenalty { get; set; } = 0;
    /// <summary>
    /// 限制一次请求中模型生成 completion 的最大 token 数。输入 token 和输出 token 的总长度受模型的上下文长度的限制。
    /// default:4096
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public long MaxTokens { get; set; } = 4096;
    /// <summary>
    /// 介于 -2.0 和 2.0 之间的数字。如果该值为正，那么新 token 会根据其是否已在已有文本中出现受到相应的惩罚，从而增加模型谈论新主题的可能性。
    /// </summary>
    [JsonPropertyName("presence_penalty")]
    public double PresencePenalty { get; set; } = 0;

    /// <summary>
    /// Up to 16 sequences where the API will stop generating further tokens.
    /// </summary>
    public List<string>? Stop { get; set; } 
    /// <summary>
    /// 如果设置为 True，将会以 SSE（server-sent events）的形式以流式发送消息增量。消息流以 data: [DONE] 结尾。
    /// </summary>
    [JsonInclude]
    public bool Stream { get; set; } 

    /// <summary>
    /// suffix
    /// </summary>
    public string? Suffix { get; set; }

    [JsonPropertyName("stream_options")]
    public StreamOptions? StreamOptions { get; set; }
    /// <summary>
    /// 采样温度，介于 0 和 2 之间。更高的值，如 0.8，会使输出更随机，而更低的值，如 0.2，会使其更加集中和确定。 我们通常建议可以更改这个值或者更改 top_p，但不建议同时对两者进行修改。
    public double Temperature { get; set; } = 1;
    /// <summary>
    /// 作为调节采样温度的替代方案，模型会考虑前 top_p 概率的 token 的结果。所以 0.1 就意味着只有包括在最高 10% 概率中的 token 会被考虑。 我们通常建议修改这个值或者更改 temperature，但不建议同时对两者进行修改。
    /// </summary>
    [JsonPropertyName("top_p")]
    public long TopP { get; set; } = 1;

    /// <summary>
    /// <=20
    /// </summary>
    [JsonPropertyName("logprobs")]
    public int? Logprobs { get; set; } = 0;
}
