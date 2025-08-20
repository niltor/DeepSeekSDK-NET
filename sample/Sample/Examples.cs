using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using DeepSeek.Core;
using DeepSeek.Core.Models;

namespace Sample;

internal class Examples
{
    public static JsonSchemaExporterOptions exporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (context, schema) =>
        {
            // Determine if a type or property and extract the relevant attribute provider.
            ICustomAttributeProvider? attributeProvider = context.PropertyInfo is not null
                ? context.PropertyInfo.AttributeProvider
                : context.TypeInfo.Type;

            // Look up any description attributes.
            DescriptionAttribute? descriptionAttr = attributeProvider
                ?.GetCustomAttributes(inherit: true)
                .Select(attr => attr as DescriptionAttribute)
                .FirstOrDefault(attr => attr is not null);

            // Apply description attribute to the generated schema.
            if (descriptionAttr != null)
            {
                if (schema is not JsonObject jObj)
                {
                    // Handle the case where the schema is a Boolean.
                    JsonValueKind valueKind = schema.GetValueKind();
                    Debug.Assert(valueKind is JsonValueKind.True or JsonValueKind.False);
                    schema = jObj = new JsonObject();
                    if (valueKind is JsonValueKind.False)
                    {
                        jObj.Add("not", true);
                    }
                }

                jObj.Insert(0, "description", descriptionAttr.Description);
            }

            return schema;
        },
    };

    public static async Task CallFunctionExampleAsync(DeepSeekClient client)
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default;
        // important to set this option to true

        var request = new ChatRequest
        {
            Messages = [Message.NewUserMessage("What is the weather in New York today?")],
            Model = DeepSeekModels.ChatModel,
            Stream = true,
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
                        Parameters = options.GetJsonSchemaAsNode(
                            typeof(WeatherDto),
                            exporterOptions
                        ),
                    },
                },
            ],
        };
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

        request.Messages.Add(message);
        if (message.ToolCalls != null && message.ToolCalls.Count > 0)
        {
            var tool = message.ToolCalls.FirstOrDefault();
            if (tool?.Function.Name == "GetWeather")
            {
                var weatherDto = JsonSerializer.Deserialize<WeatherDto>(
                    tool.Function.Arguments.ToString(),
                    options
                );

                var toolResult = Functions.GetWeather(weatherDto);

                request.Messages.Add(Message.NewToolMessage(toolResult, tool.Id));
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

    // chat
    public static async Task ChatAsync(DeepSeekClient client)
    {
        var request = new ChatRequest
        {
            Messages =
            [
                Message.NewSystemMessage("你是一个语言翻译家"),
                Message.NewUserMessage(
                    """
                    请翻译'它们害怕极了！'为英语!,返回json，格式为:
                    {
                        "text":[],
                        "translate":""
                    }
                    """
                ),
            ],
            ResponseFormat = new ResponseFormat { Type = ResponseFormatTypes.JsonObject },
            Model = DeepSeekModels.ChatModel,
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
    public static async Task CompletionsAsync(DeepSeekClient client)
    {
        var request = new CompletionRequest
        {
            Prompt = ".Net and C# is prefect, because",
            Model = DeepSeekModels.ChatModel,
            MaxTokens = 100,
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
    public static async Task GetUserBalanceAsync(DeepSeekClient client)
    {
        var balance = await client.GetUserBalanceAsync(new CancellationToken());
        if (balance is null)
        {
            Console.WriteLine(client.ErrorMsg);
            return;
        }
        Console.WriteLine(balance.BalanceInfos.First().TotalBalance);
    }

    public static async Task TestLocalAsync()
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

        var res = await localClient.ChatAsync(
            new ChatRequest { Messages = new List<Message> { Message.NewUserMessage("hello") } },
            new CancellationToken()
        );

        Console.WriteLine(res?.Choices.FirstOrDefault()?.Message);
    }

    public static async Task StreamChatAsync(DeepSeekClient client)
    {
        var request = new ChatRequest
        {
            Messages = [Message.NewUserMessage("which is greater between 9.11 and 9.8?")],
            //Model = DeepSeekModels.ChatModel
            Model = DeepSeekModels.ReasonerModel,
        };

        var choices = client.ChatStreamAsync(request, new CancellationToken());
        if (choices is null)
        {
            Console.WriteLine(client.ErrorMsg);
            return;
        }
        await foreach (var choice in choices)
        {
            // output Cot
            if (!string.IsNullOrWhiteSpace(choice.Delta?.ReasoningContent))
            {
                Console.WriteLine(choice.Delta.ReasoningContent);
            }
            else
            {
                Console.Write(choice.Delta?.Content);
            }
        }
        Console.WriteLine();
    }
}
