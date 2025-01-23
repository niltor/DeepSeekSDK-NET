# DeepSeekSDK-NET

![NuGet Version](https://img.shields.io/nuget/v/Ater.DeepSeek.Core)

[DeepSeek](https://www.deepseek.com) API SDK specifically for .NET developers

[中文文档](./README_cn.md)

## 🚀 Features

- [x] List models
- [x] Chat & Chat streaming
- [x] Completions & Completions streaming (beta)
- [x] User balance

## Usage Requirements

## Usage

Please go to [official website](https://platform.deepseek.com/), register and apply for DeepSeek's ApiKey

Supported .NET version: .NET8

### Install Nuget package

[Ater.DeepSeek.Core](https://www.nuget.org/packages/Ater.DeepSeek.Core)

```shell
dotnet add package Ater.DeepSeek.Core
```

### Instantiate `DeepSeekClient`

Two methods are provided for instantiation:

```csharp
public DeepSeekClient(string apiKey);
public DeepSeekClient(HttpClient http, string apiKey);
```

The first type only requires providing the 'apiKey' to create an instance;

The second method provides a `HttpClient` parameter, which is suitable for maintaining the `HttpClient` through the `HttpClientFactory` and then instance it.

> [!NOTE]
The default timeout for internal HttpClient is 120 seconds, which can be set before sending the request using the 'SetTimeout()' method, or by using the 'CancellationTokeSource' to set the timeout for specific requests.

> [!TIP]
> If you want to call a local model, try customizing `HttpClient` and setting `BaseAddress` to the local address.

### Calling method

Three asynchronous methods are provided:

```csharp
Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken);

Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken);

Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken);

Task<ChatResponse?> CompletionsAsync(CompletionRequest request, CancellationToken cancellationToken);

Task<IAsyncEnumerable<Choice>?> CompletionsStreamAsync(CompletionRequest request, CancellationToken cancellationToken);

Task<UserResponse?> GetUserBalanceAsync(CancellationToken cancellationToken);

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
> More [usage example](https://github.com/niltor/DeepSeekSDK-NET/tree/dev/sample/Sample)
