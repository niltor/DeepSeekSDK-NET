using DeepSeek.Core;
using Microsoft.Extensions.Configuration;
using Sample;

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

await Examples.CallFunctionExampleAsync(client);

//await ChatAsync(client);
//await CompletionsAsync(client);
//await GetUserBalanceAsync(client);
//await StreamChatAsync(client);
//await TestLocalAsync();

Console.WriteLine("done");
Console.ReadLine();
