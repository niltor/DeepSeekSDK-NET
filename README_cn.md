# DeepSeekSDK-NET

![NuGet Version](https://img.shields.io/nuget/v/Ater.DeepSeek.Core)

专门为.NET开发者提供的 [DeepSeek](https://www.deepseek.com) API SDK.

[English Docs](./README.md)

## 🚀 功能特性

- [x] 列出模型
- [x] 对话补全(包含流式)
- [x] FIM实例(包含流式)
- [x] 查询余额

## 使用

请到[官方网站](https://platform.deepseek.com/)，注册并申请DeepSeek的`ApiKey`.

.NET版本:.NET8

### 安装Nugget包

[Ater.DeepSeek.Core](https://www.nuget.org/packages/Ater.DeepSeek.Core)

```shell
dotnet add package Ater.DeepSeek.Core
```

### 实例化`DeepSeekClient`

提供了两种方式进行实例化:

```csharp
public DeepSeekClient(string apiKey)；
public DeepSeekClient(HttpClient http, string apiKey);
```

第一种只需要提供`apiKey`即可创建实例;

第二种提供了`HttpClient`参数，适合通过`HttpClientFactory`来维护`HttpClient`，然后进行实例化。

> [!NOTE]
> 内部HttpClient的超时时间默认为120秒，可通过`SetTimeout()`方法在发送请求前设置，或通过`CancellationTokenSource`设置具体请求的超时时间。

> [!TIP]
> 如果你想调用本地模型，可尝试自定义`HttpClient`，并设置`BaseAddress`为本地地址。

### 调用方法

一共提供了三个异步方法：

```csharp
Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken);

Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken);

Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken);

Task<ChatResponse?> CompletionsAsync(CompletionRequest request, CancellationToken cancellationToken);

Task<IAsyncEnumerable<Choice>?> CompletionsStreamAsync(CompletionRequest request, CancellationToken cancellationToken);

Task<UserResponse?> GetUserBalanceAsync(CancellationToken cancellationToken);
```

### 获取模型列表示例

```csharp
// 通过apiKey创建实例
var client = new DeepSeekClient(apiKey);

var modelResponse = await client.ListModelsAsync(new CancellationToken());
if (modelResponse is null)
{
    Console.WriteLine(client.ErrorMsg);
    return;
}
foreach (var model in modelResponse.Data)
{
    Console.WriteLine(model);
}
```

### 获取对话示例

```csharp
// 通过apiKey创建实例
var client = new DeepSeekClient(apiKey);
// 构造请求体
var request = new ChatRequest
{
    Messages = [
        Message.NewSystemMessage("你是一个语言翻译家"),
        Message.NewUserMessage("请翻译'它们害怕极了！'为英语!")
    ],
    // 指定模型
    Model = Constant.Model.ChatModel
};

var chatResponse = await client.ChatAsync(request, new CancellationToken());
if (chatResponse is null)
{
    Console.WriteLine(client.ErrorMsg);
}
Console.WriteLine(chatResponse?.Choices.First().Message?.Content);
```

### 获取对话(Stream)

```csharp
// 通过apiKey创建实例
var client = new DeepSeekClient(apiKey);
// 构造请求体
var request = new ChatRequest
{
    Messages = [
        Message.NewSystemMessage("你是一个语言翻译家"),
        Message.NewUserMessage("请翻译'它们害怕极了！'为英语!")
    ],
    // 指定模型
    Model = Constant.Model.ChatModel
};

var choices = await client.ChatStreamAsync(request, new CancellationToken());
if (choices is null)
{
    Console.WriteLine(client.ErrorMsg);
    return;
}
await foreach (var choice in choices)
{
    Console.Write(choice.Delta?.Content);
}
Console.WriteLine();
```

> [!TIP]
> 更多[使用示例](https://github.com/niltor/DeepSeekSDK-NET/tree/dev/sample/Sample).
