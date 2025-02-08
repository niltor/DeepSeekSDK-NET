using DeepSeek.Core;
using DeepSeek.Core.Models;
using Microsoft.Extensions.Configuration;


// 从appsettings.json读取秘钥
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

// create client
var client = new DeepSeekClient(apiKey);

// get models
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

//await ChatAsync(client);
//await CompletionsAsync(client);
//await GetUserBalanceAsync(client);
await StreamChatAsync(client);
//await TestLocalAsync();

Console.WriteLine("done");
Console.ReadLine();

// stream chat using DeepSeek-R1
static async Task StreamChatAsync(DeepSeekClient client)
{
    var request = new ChatRequest
    {
        Messages = [
            Message.NewUserMessage("which is greater between 9.11 and 9.8?")
        ],
        Model = DeepSeekModels.ChatModel
        //Model = DeepSeekModels.ReasonerModel
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
}

// chat
static async Task ChatAsync(DeepSeekClient client)
{
    var request = new ChatRequest
    {
        Messages = [
            Message.NewSystemMessage("你是一个语言翻译家"),
            Message.NewUserMessage("""
                请翻译'它们害怕极了！'为英语!,返回json，格式为:
                {
                    "text":"",
                    "translate":""
                }
                """)
        ],
        ResponseFormat = new ResponseFormat
        {
            Type = ResponseFormatTypes.JsonObject
        },
        Model = DeepSeekModels.ChatModel
    };


    var chatResponse = await client.ChatAsync(request, new CancellationToken());

    if (chatResponse is null)
    {
        Console.WriteLine(client.ErrorMsg);
    }
    // usage
    Console.WriteLine("use token:" + chatResponse?.Usage?.TotalTokens);
    // result
    Console.WriteLine(chatResponse?.Choices.FirstOrDefault()?.Message?.Content);
}

// completions
static async Task CompletionsAsync(DeepSeekClient client)
{
    var request = new CompletionRequest
    {
        Prompt = ".Net and C# is prefect, because",
        Model = DeepSeekModels.ChatModel,
        MaxTokens = 100
    };
    var response = await client.CompletionsAsync(request, new CancellationToken());
    if (response is null)
    {
        Console.WriteLine(client.ErrorMsg);
        return;
    }
    // usage
    Console.WriteLine(response?.Usage?.TotalTokens);
    // result
    Console.WriteLine(response?.Choices.First().Text);
}

// user balance
static async Task GetUserBalanceAsync(DeepSeekClient client)
{
    var balance = await client.GetUserBalanceAsync(new CancellationToken());
    if (balance is null)
    {
        Console.WriteLine(client.ErrorMsg);
        return;
    }
    Console.WriteLine(balance.BalanceInfos.First().TotalBalance);
}

static async Task TestLocalAsync()
{
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

    Console.WriteLine(res?.Choices.FirstOrDefault()?.Message);
}