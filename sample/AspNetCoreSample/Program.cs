using DeepSeek.AspNetCore;
using DeepSeek.Core;
using DeepSeek.Core.Adapters;
using DeepSeek.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

var apiKey = builder.Configuration["apiKey"]
    ?? throw new InvalidOperationException("The 'apiKey' configuration value is required.");
builder.Services.AddDeepSeek(option =>
{
    option.BaseAddress = new Uri("https://api.deepseek.com");
    option.Timeout = TimeSpan.FromSeconds(300);
    option.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
});

// use Microsoft.Extensions.AI IChatClient
builder.Services.AddChatClient(sp => new DeepSeekChatClient(apiKey));

var app = builder.Build();

app.MapGet("/test", async ([FromServices] DeepSeekClient client, CancellationToken token) =>
{
    var res = await client.ChatAsync(new ChatRequest
    {
        Messages =
        [
            Message.NewUserMessage("Why dotnet is good?")
        ],
        MaxTokens = 200,
        Stream = false
    }, token);

    return res?.Choices.First().Message?.Content;
});

app.MapGet("/chat", async (HttpContext context, [FromServices] DeepSeekClient client, CancellationToken token) =>
{
    context.Response.ContentType = "text/text;charset=utf-8";
    try
    {
        var choices = client.ChatStreamAsync(new ChatRequest
        {
            Messages =
            [
                Message.NewUserMessage("Why dotnet is good?")
            ],
            MaxTokens = 200,
            Stream = true
        }, token);

        if (choices != null)
        {
            await foreach (var choice in choices)
            {
                //Console.WriteLine(choice.Delta?.Content);
                await context.Response.WriteAsync(choice.Delta!.Content);
            }
        }
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync("request failed：" + ex.Message);
    }
    await context.Response.CompleteAsync();
});

// test IChatClient
app.MapGet("/ichat", async (HttpContext context, [FromServices] IChatClient chatClient, CancellationToken token) =>
{
    context.Response.ContentType = "text/text;charset=utf-8";
    try
    {
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Why dotnet is good?")
        };

        var response = chatClient.GetStreamingResponseAsync(messages, new ChatOptions
        {
            ModelId = DeepSeekModels.Flash,
            MaxOutputTokens = 200,
        }, token);
        if (response != null)
        {
            await foreach (var item in response)
            {
                await context.Response.WriteAsync(item.Text);
            }
        }
    }
    catch (Exception ex)
    {
        await context.Response.WriteAsync("request failed：" + ex.Message);
    }
    await context.Response.CompleteAsync();
});

app.Run();
