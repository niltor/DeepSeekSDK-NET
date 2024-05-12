# DeepSeekSDK-NET

![NuGet Version](https://img.shields.io/nuget/v/Ater.DeepSeek.Core)

专门为.NET开发者提供的 [DeepSeek](https://www.deepseek.com) API SDK.

[English Docs](./README.md)

## 🚀 功能特性

- [x] 列出模型
- [x] 对话补全
- [x] 对话补全(流式处理)

## 使用要求

请到[官方网站](https://platform.deepseek.com/)，注册并申请DeepSeek的`ApiKey`.

支持.NET版本:.NET8

> [!TIP]
> 可自由fork仓库，以支持其他版本

## 使用

### 实例化`DeepSeekClient`

提供了两种方式进行实例化:

```csharp
public DeepSeekClient(string apiKey)；
public DeepSeekClient(HttpClient http, string apiKey);
```

第一种只需要提供`apiKey`即可创建实例;

第二种提供了`HttpClient`参数，适合通过`HttpClientFactory`来维护`HttpClient`，然后进行实例化。

### 调用方法

一共提供了三个异步方法：

```csharp
Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken);
Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken);
Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken)
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
> 同时可参考本仓库中的[使用示例](https://github.com/niltor/DeepSeekSDK-NET/tree/dev/sample/Sample).
