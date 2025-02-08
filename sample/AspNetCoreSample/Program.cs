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