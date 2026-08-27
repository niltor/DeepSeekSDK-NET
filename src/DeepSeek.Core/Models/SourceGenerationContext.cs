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
[JsonSerializable(typeof(ResponseInputItem[]))]
[JsonSerializable(typeof(ResponseOutputItem))]
[JsonSerializable(typeof(ResponseContentPart))]
[JsonSerializable(typeof(List<ResponseContentPart>))]
[JsonSerializable(typeof(ChatMessageContentPart))]
[JsonSerializable(typeof(List<ChatMessageContentPart>))]
[JsonSerializable(typeof(List<ToolCalls>))]
[JsonSerializable(typeof(FileObject))]
[JsonSerializable(typeof(FileListResponse))]
[JsonSerializable(typeof(FileDeletionResponse))]
[JsonSerializable(typeof(FileDeleteResponse))]
[JsonSerializable(typeof(UserResponse))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
public partial class SourceGenerationContext : JsonSerializerContext
{
}
