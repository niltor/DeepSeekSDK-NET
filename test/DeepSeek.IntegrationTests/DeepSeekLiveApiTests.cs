using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using DeepSeek.Core;
using DeepSeek.Core.Models;
using Xunit;

namespace DeepSeek.IntegrationTests;

public class DeepSeekLiveApiTests : IClassFixture<DeepSeekIntegrationFixture>
{
    private static readonly JsonSchemaExporterOptions ExporterOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
        TransformSchemaNode = (context, schema) =>
        {
            ICustomAttributeProvider? attributeProvider = context.PropertyInfo is not null
                ? context.PropertyInfo.AttributeProvider
                : context.TypeInfo.Type;

            DescriptionAttribute? descriptionAttr = attributeProvider
                ?.GetCustomAttributes(inherit: true)
                .Select(attr => attr as DescriptionAttribute)
                .FirstOrDefault(attr => attr is not null);

            if (descriptionAttr is null)
            {
                return schema;
            }

            if (schema is not JsonObject jsonObject)
            {
                var valueKind = schema.GetValueKind();
                Debug.Assert(valueKind is JsonValueKind.True or JsonValueKind.False);
                schema = jsonObject = new JsonObject();
                if (valueKind is JsonValueKind.False)
                {
                    jsonObject.Add("not", true);
                }
            }

            jsonObject.Insert(0, "description", descriptionAttr.Description);
            return schema;
        },
    };

    private readonly DeepSeekClient _client;
    private readonly DeepSeekIntegrationFixture _fixture;

    public DeepSeekLiveApiTests(DeepSeekIntegrationFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    [Fact]
    public async Task ListModelsAsync_ReturnsV4Models()
    {
        var response = await _client.ListModelsAsync(_fixture.CreateToken());

        Assert.NotNull(response);
        Assert.Contains(response!.Data, model => model.Id == DeepSeekModels.Flash);
        Assert.Contains(response.Data, model => model.Id == DeepSeekModels.Pro);
    }

    [Fact]
    public async Task ChatAsync_ReturnsContent()
    {
        var request = new ChatRequest
        {
            Model = DeepSeekModels.Flash,
            MaxTokens = 32,
            Temperature = 0,
            Messages = [Message.NewUserMessage("Reply with exactly: chat-ok")],
        };

        var response = await _client.ChatAsync(request, _fixture.CreateToken());

        Assert.NotNull(response);
        Assert.NotEmpty(response!.Choices);
        Assert.False(string.IsNullOrWhiteSpace(response.Choices[0].Message?.Content));
    }

    [Fact]
    public async Task ChatStreamAsync_ReturnsStreamChunks()
    {
        var request = new ChatRequest
        {
            Model = DeepSeekModels.Flash,
            MaxTokens = 32,
            Temperature = 0,
            Messages = [Message.NewUserMessage("Reply with exactly: stream-ok")],
        };

        var stream = _client.ChatStreamAsync(request, _fixture.CreateToken());
        Assert.NotNull(stream);

        var chunks = new List<string>();
        await foreach (var choice in stream!)
        {
            var chunk = choice.Delta?.Content;
            if (string.IsNullOrWhiteSpace(chunk))
            {
                chunk = choice.Delta?.ReasoningContent;
            }

            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
        }

        Assert.NotEmpty(chunks);
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(chunks)));
    }

    [Fact]
    public async Task CompletionsAsync_ReturnsText()
    {
        var request = new CompletionRequest
        {
            Model = DeepSeekModels.Flash,
            Prompt = "Complete with one short token: sdk-",
            MaxTokens = 16,
            Temperature = 0,
        };

        var response = await _client.CompletionsAsync(request, _fixture.CreateToken());

        Assert.NotNull(response);
        Assert.NotEmpty(response!.Choices);
        Assert.False(string.IsNullOrWhiteSpace(response.Choices[0].Text));
    }

    [Fact]
    public async Task CompletionsStreamAsync_ReturnsTextChunks()
    {
        var request = new CompletionRequest
        {
            Model = DeepSeekModels.Flash,
            Prompt = "Complete with one short token: stream-",
            MaxTokens = 16,
            Temperature = 0,
        };

        var stream = _client.CompletionsStreamAsync(request, _fixture.CreateToken());
        Assert.NotNull(stream);

        var chunks = new List<string>();
        await foreach (var choice in stream!)
        {
            if (!string.IsNullOrWhiteSpace(choice.Text))
            {
                chunks.Add(choice.Text);
            }
        }

        Assert.NotEmpty(chunks);
        Assert.False(string.IsNullOrWhiteSpace(string.Concat(chunks)));
    }

    [Fact]
    public async Task GetUserBalanceAsync_ReturnsBalance()
    {
        var response = await _client.GetUserBalanceAsync(_fixture.CreateToken());

        Assert.NotNull(response);
        Assert.NotEmpty(response!.BalanceInfos);
    }

    [Fact]
    public async Task ChatAsync_WithFunctionCall_CompletesToolRoundtrip()
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default;
        var request = new ChatRequest
        {
            Model = DeepSeekModels.Flash,
            Messages = [Message.NewUserMessage("What is the weather in New York today?")],
            Tools =
            [
                new Tool
                {
                    Function = new RequestFunction
                    {
                        Name = "GetWeather",
                        Description = "Get the weather for a city on a given date.",
                        Parameters = options.GetJsonSchemaAsNode(typeof(WeatherDto), ExporterOptions),
                    },
                },
            ],
        };

        var response = await _client.ChatAsync(request, _fixture.CreateToken());

        Assert.NotNull(response);
        var message = response!.Choices[0].Message;
        Assert.NotNull(message);
        Assert.NotNull(message!.ToolCalls);
        var toolCall = Assert.Single(message.ToolCalls!);
        Assert.Equal("GetWeather", toolCall.Function.Name);

        var weatherDto = JsonSerializer.Deserialize<WeatherDto>(toolCall.Function.Arguments, options);
        Assert.NotNull(weatherDto);

        request.Messages.Add(message);
        request.Messages.Add(Message.NewToolMessage(GetWeather(weatherDto!), toolCall.Id));

        var toolResponse = await _client.ChatAsync(request, _fixture.CreateToken());

        Assert.NotNull(toolResponse);
        Assert.False(string.IsNullOrWhiteSpace(toolResponse!.Choices[0].Message?.Content));
    }

    private static string GetWeather(WeatherDto dto)
    {
        return $"The weather in {dto.City} on {dto.Date:yyyy-MM-dd} is sunny with a high of 25°C and a low of 15°C.";
    }

    private sealed class WeatherDto
    {
        public required string City { get; set; }

        [Description("The date, default is today's date")]
        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    }
}