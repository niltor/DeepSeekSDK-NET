# DeepSeekSDK-NET

![NuGet Version](https://img.shields.io/nuget/v/Ater.DeepSeek.Core)

专门为.NET开发者提供的 [DeepSeek](https://www.deepseek.com) API SDK.

[English Docs](./README.md)

## 🚀 功能特性

- [x] 列出模型
- [x] 对话补全(包含流式)
- [x] FIM实例(包含流式)
- [x] 查询余额
- [x] 支持调用本地模型
- [x] 对ASP.NET Core的集成支持
- [x] 函数调用
- [x] 兼容OpenAI格式的 Responses API（包含语义化 SSE 流式返回）
- [x] 支持Microsoft.Extensions.AI IChatClient

## 使用

请到[官方网站](https://platform.deepseek.com/)，注册并申请DeepSeek的`ApiKey`.

.NET版本:.NET8

### 安装Nuget包

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

> [!IMPORTANT]
> DeepSeek 现已支持 `deepseek-v4-pro` 与 `deepseek-v4-flash`，访问新模型时 `base_url` 不变。
> 旧模型 ID `deepseek-chat` 与 `deepseek-reasoner` 将在 2026-07-24 停止使用。
> 过渡期内，这两个旧 ID 分别指向 `deepseek-v4-flash` 的非思考模式与思考模式。

### 调用方法

`DeepSeekClient`类提供了异步方法来调用DeepSeek的API:

```csharp
Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken);

Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken);

Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken);

Task<ChatResponse?> CompletionsAsync(CompletionRequest request, CancellationToken cancellationToken);

Task<IAsyncEnumerable<Choice>?> CompletionsStreamAsync(CompletionRequest request, CancellationToken cancellationToken);

Task<ResponseResult?> ResponseAsync(ResponseRequest request, CancellationToken cancellationToken);

IAsyncEnumerable<ResponseStreamEvent> ResponseStreamAsync(ResponseRequest request, CancellationToken cancellationToken);

Task<UserResponse?> GetUserBalanceAsync(CancellationToken cancellationToken);
```

### Responses API

DeepSeek 的 Responses API 遵循 OpenAI Responses 格式，调用地址为 `POST /responses`。需要读取
`output`、思考内容、函数调用、用量或原始 SSE 事件时，可直接使用 `ResponseRequest`、`ResponseResult`
和 `ResponseStreamEvent`：

```csharp
var response = await client.ResponseAsync(new ResponseRequest
{
    Model = DeepSeekModels.Flash,
    Instructions = "你是一个简洁的助手。",
    Input = "法国的首都是什么？",
    Reasoning = new ResponseReasoningOptions { Effort = "high" },
}, cancellationToken);

var text = response?.Output
    .Where(item => item.Type == ResponseInputItemTypes.Message)
    .SelectMany(item => item.Content ?? [])
    .Where(part => part.Type == ResponseContentPartTypes.OutputText)
    .Select(part => part.Text)
    .FirstOrDefault();
```

Responses 流式返回使用语义化 SSE 事件，最后以 `response.completed`、`response.incomplete` 或
`response.failed` 结束，不使用 Chat Completions 的 `[DONE]`：

```csharp
await foreach (var responseEvent in client.ResponseStreamAsync(
    new ResponseRequest
    {
        Model = DeepSeekModels.Flash,
        Input = "从一数到三。",
    }, cancellationToken))
{
    if (responseEvent.Type == ResponseEventTypes.OutputTextDelta)
    {
        Console.Write(responseEvent.Delta);
    }
}
```

DeepSeek Responses API 是无状态的，每次请求都需要在 `input` 中传回完整历史；不支持服务端的
`previous_response_id`、`conversation` 和响应存储。详见[官方 Responses API 指南](https://api-docs.deepseek.com/zh-cn/guides/responses_api)
和[创建 Response API 文档](https://api-docs.deepseek.com/zh-cn/api/create-response)。

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
    Model = DeepSeekModels.Flash
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
    Model = DeepSeekModels.Flash
};

var choices = client.ChatStreamAsync(request, new CancellationToken());
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

### 函数调用示例

比如我有本地函数定义:

```csharp
internal class Functions
{
    public static string GetWeather(WeatherDto dto)
    {
        return $"The weather in {dto.City} on {dto.Date:yyyy-MM-dd} is sunny with a high of 25°C and a low of 15°C.";
    }
}

internal class WeatherDto
{
    public required string City { get; set; }

    [Description("The date,default is today date")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}
```

在使用LLM时，传入函数定义:

```csharp
public static async Task CallFunctionExampleAsync(DeepSeekClient client)
{
    JsonSerializerOptions options = JsonSerializerOptions.Default;
    // 必须的配置，否则生成的格式报错
    JsonSchemaExporterOptions exporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
    };
    var request = new ChatRequest
    {
        Messages = [Message.NewUserMessage("What is the weather in New York today?")],
        Model = DeepSeekModels.Flash,
        Stream = true,
        // 添加tools的定义
        Tools =
        [
            new Tool
            {
                Function = new RequestFunction
                {
                    Name = "JustUselessFunction",
                    Description = "nothing to do",
                },
            },
            new Tool
            {
                Function = new RequestFunction
                {
                    Name = "GetWeather",
                    Description = "get the weather",
                    // 参数的json schema
                    Parameters = options.GetJsonSchemaAsNode(
                        typeof(WeatherDto),
                        exporterOptions
                    ),
                },
            },
        ],
    };
    // 第一次返回LLM会识别要调用函数，返回函数内容
    var response = await client.ChatAsync(request, new CancellationToken());
    if (response is null)
    {
        Console.WriteLine(client.ErrorMsg);
        return;
    }

    var message = response.Choices[0].Message;
    if (message == null)
    {
        Console.WriteLine("no message");
        return;
    }
    request.Messages.Add(message); // 必须将消息添加到请求中，以便后续调用函数时使用。
    if (message.ToolCalls != null && message.ToolCalls.Count > 0)
    {
        // 如果有函数调用，则使用本地函数获得内容
        var tool = message.ToolCalls.FirstOrDefault();
        if (tool?.Function.Name == "GetWeather")
        {
            var weatherDto = JsonSerializer.Deserialize<WeatherDto>(
                tool.Function.Arguments.ToString(),
                options
            );

            var toolResult = Functions.GetWeather(weatherDto);
            // 将本地函数调用结果添加到消息中
            request.Messages.Add(Message.NewToolMessage(toolResult, tool.Id));

            // 再次使用LLM处理结果
            var toolResponse = await client.ChatAsync(request, new CancellationToken());
            if (toolResponse is null)
            {
                Console.WriteLine(client.ErrorMsg);
                return;
            }

            Console.WriteLine(toolResponse.Choices[0].Message?.Content);
        }
    }
    else
    {
        Console.WriteLine("No tool calls found in the response.");
    }
}

```

### 本地模型调用示例

```csharp
// use local models api
var httpClient = new HttpClient
{
    // set your local api address
    BaseAddress = new Uri("http://localhost:5000"),
    Timeout = TimeSpan.FromSeconds(300),
};
// if have api key
// httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + "your_token");

var localClient = new DeepSeekClient(httpClient);
localClient.SetChatEndpoint("/chat");
localClient.SetCompletionEndpoint("/completions");

var res = await localClient.ChatAsync(new ChatRequest
{
    Messages = new List<Message>
    {
        Message.NewUserMessage("hello")
    }
}, new CancellationToken());
return res?.Choices.First().Message?.Content;
```

> [!TIP]
> 更多[使用示例](https://github.com/niltor/DeepSeekSDK-NET/tree/dev/sample/Sample).
>


## Microsoft.Extensions.AI Integration

现有的 `DeepSeekChatClient` 使用 Chat Completions。如果要通过 `Microsoft.Extensions.AI` 使用 Responses API，
请使用 `DeepSeekResponsesChatClient`：

```csharp
using DeepSeek.Core.Adapters;
using Microsoft.Extensions.AI;

builder.Services.AddChatClient(sp => new DeepSeekResponsesChatClient(apiKey));
```

该适配器会将输出文本映射为 `TextContent`，思考内容映射为 `TextReasoningContent`，函数调用映射为
`FunctionCallContent`，用量映射为 `UsageDetails`；原始 `ResponseResult` 可从 `ChatResponse.RawRepresentation` 获取。

## 在ASP.NET Core中使用

### 安装`Ater.DeepSeek.AspNetCore`包

```shell
dotnet add package Ater.DeepSeek.AspNetCore
```

### 示例代码

```csharp
using DeepSeek.AspNetCore;
using DeepSeek.Core;
using DeepSeek.Core.Models;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["DeepSeekApiKey"];
builder.Services.AddDeepSeek(option =>
{
    option.BaseAddress = new Uri("https://api.deepseek.com");
    option.Timeout = TimeSpan.FromSeconds(300);
    option.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
});

var app = builder.Build();

app.MapGet("/test", async ([FromServices] DeepSeekClient client) =>
{
    var res = await client.ChatAsync(new ChatRequest
    {
        Messages = new List<Message>
        {
            Message.NewUserMessage("Why dotnet is good?")
        },
        MaxTokens = 200
    }, new CancellationToken());

    return res?.Choices.First().Message?.Content;
});

app.Run();
```

### 流式返回示例

```csharp
app.MapGet("/chat", async (HttpContext context, [FromServices] DeepSeekClient client, CancellationToken token) =>
{
    context.Response.ContentType = "text/text;charset=utf-8";
    try
    {
        var choices = client.ChatStreamAsync(new ChatRequest
        {
            Messages = new List<Message>
            {
                Message.NewUserMessage("Why dotnet is good?")
            },
            MaxTokens = 200
        }, token);

        if (choices != null)
        {
            await foreach (var choice in choices)
            {
                await context.Response.WriteAsync(choice.Delta!.Content);
            }
        }
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync("暂时无法提供服务" + ex.Message);
    }
    await context.Response.CompleteAsync();
});
```

> [!TIP]
> More [usage example](https://github.com/niltor/DeepSeekSDK-NET/tree/dev/sample/AspNetCoreSample)
