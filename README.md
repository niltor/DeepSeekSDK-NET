# DeepSeekSDK-NET

[DeepSeek](https://www.deepseek.com) API SDK specifically for .NET developers

[中文文档](./README_cn.md)

## 🚀 Features

- [x] List models
- [x] Dialogue completion
- [x] Dialog completion (streaming processing)

## Usage Requirements

Please go to [official website](https://platform.deepseek.com/), register and apply for DeepSeek's ApiKey

Supported .NET version: .NET8

> [!TIP]
> Free to fork the repository to support other versions

## Usage

### Instantiate `DeepSeekClient`

Two methods are provided for instantiation:

```csharp
public DeepSeekClient(string apiKey);
public DeepSeekClient(HttpClient http, string apiKey);
```

The first type only requires providing the 'apiKey' to create an instance;

The second method provides a `HttpClient` parameter, which is suitable for maintaining the `HttpClient` through the `HttpClientFactory` and then materializing it.

### Calling method

Three asynchronous methods are provided:

```csharp
Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken);
Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken);
Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken)
```

### List Models Sample

```csharp
// Create an instance using the apiKey
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

### Chat Examples

```csharp
// Create an instance using the apiKey
var client = new DeepSeekClient(apiKey);
// Construct the request body
var request = new ChatRequest
{
    Messages = [
        Message.NewSystemMessage("You are a language translator"),
        Message.NewUserMessage("Please translate 'They are scared! ' into English!")
    ],
    // Specify the model
    Model = Constant.Model.ChatModel
};

var chatResponse = await client.ChatAsync(request, new CancellationToken());
if (chatResponse is null)
{
    Console.WriteLine(client.ErrorMsg);
}
Console.WriteLine(chatResponse?.Choices.First().Message?.Content);
```

### Chat Examples (Stream)

```csharp
// Create an instance using the apiKey
var client = new DeepSeekClient(apiKey);
// Construct the request body
var request = new ChatRequest
{
    Messages = [
        Message.NewSystemMessage("You are a language translator"),
        Message.NewUserMessage("Please translate 'They are scared! ' into English!")
    ],
    // Specify the model
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
> You can also refer to the [usage example](https://github.com/niltor/DeepSeekSDK-NET/tree/dev/sample/Sample) in this repository
