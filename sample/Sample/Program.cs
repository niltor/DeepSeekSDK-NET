// 读取appsettings.json
using DeepSeek.Core;
using DeepSeek.Core.Models;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>();

var configuration = builder.Build();

var apiKey = configuration["apiKey"];

if (apiKey == null)
{
    Console.WriteLine("apiKey is null");
    return;
}


// 创建DeepSeekClient
var client = new DeepSeekClient(apiKey);

#region 示例1：获取模型列表
var models = await client.ListModelsAsync(new CancellationToken());
if (models is null)
{
    Console.WriteLine(client.ErrorMsg);
    return;
}
foreach (var model in models.Data)
{
    Console.WriteLine(model);
}

#endregion


var request = new ChatRequest
{
    Messages = [
        Message.NewSystemMessage("你是一个语言翻译家"),
        Message.NewUserMessage("请翻译'它们害怕极了！'为英语!")
    ],
    Model = Constant.Model.ChatModel
};

// 聊天流式输出
//await StreamChatAsync(client, request);
// 聊天等待完成
//await ChatAsync(client, request);


Console.WriteLine("done!");



static async Task StreamChatAsync(DeepSeekClient client, ChatRequest request)
{
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
}

static async Task ChatAsync(DeepSeekClient client, ChatRequest request)
{
    var chatResponse = await client.ChatAsync(request, new CancellationToken());

    if (chatResponse is null)
    {
        Console.WriteLine(client.ErrorMsg);
    }
    Console.WriteLine(chatResponse?.Choices.First().Message?.Content);
}