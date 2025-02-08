using System.Threading;
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

app.MapGet("/test", async ([FromServices] DeepSeekClient client, CancellationToken token) =>
{
    var res = await client.ChatAsync(new ChatRequest
    {
        Messages = new List<Message>
        {
            Message.NewUserMessage("Why dotnet is good?")
        },
        MaxTokens = 200
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
                //Console.WriteLine(choice.Delta?.Content);
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
app.Run();