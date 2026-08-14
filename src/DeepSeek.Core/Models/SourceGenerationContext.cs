using System.Text.Json.Serialization;

namespace DeepSeek.Core.Models;


[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(CompletionRequest))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(ModelResponse))]
[JsonSerializable(typeof(ResponseRequest))]
[JsonSerializable(typeof(ResponseResult))]
[JsonSerializable(typeof(ResponseStreamEvent))]
[JsonSerializable(typeof(ResponseInputItem))]
[JsonSerializable(typeof(List<ResponseInputItem>))]
[JsonSerializable(typeof(ResponseOutputItem))]
[JsonSerializable(typeof(ResponseContentPart))]
[JsonSerializable(typeof(UserResponse))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
