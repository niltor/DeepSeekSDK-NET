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

var request = new ChatRequest
{
    Messages = [
        Message.NewSystemMessage("你是一个编程大师"),
        Message.NewUserMessage("C#语言的特点和优势")
    ],
};

var choices = await client.ChatStreamAsync(request);

await foreach (var choice in choices)
{
    Console.Write(choice.Delta.Content);
}

Console.WriteLine();
Console.WriteLine("done!");

