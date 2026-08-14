using System.Net;
using System.Text;
using System.Text.Json;
using DeepSeek.Core;
using DeepSeek.Core.Adapters;
using DeepSeek.Core.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace DeepSeek.IntegrationTests;

public sealed class ResponsesApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new DeepSeekClient(new HttpClient())
        .JsonSerializerOptions;

    [Fact]
    public void ResponseRequest_SerializesResponsesShape()
    {
        var request = new ResponseRequest
        {
            Model = DeepSeekModels.Flash,
            Input = "Hello",
            Instructions = "Be concise.",
            Reasoning = new ResponseReasoningOptions { Effort = "high" },
            MaxOutputTokens = 512,
            Stream = true,
            Text = new ResponseTextOptions
            {
                Format = new ResponseTextFormat { Type = ResponseTextFormatTypes.JsonObject },
            },
            Tools =
            [
                new ResponseTool
                {
                    Name = "get_weather",
                    Description = "Gets the weather.",
                    Parameters = new System.Text.Json.Nodes.JsonObject
                    {
                        ["type"] = "object",
                    },
                },
            ],
            ToolChoice = "auto",
            TopLogprobs = 2,
            User = "user-1",
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        var root = document.RootElement;

        Assert.Equal("deepseek-v4-flash", root.GetProperty("model").GetString());
        Assert.Equal("Hello", root.GetProperty("input").GetString());
        Assert.Equal("Be concise.", root.GetProperty("instructions").GetString());
        Assert.Equal("high", root.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Equal(512, root.GetProperty("max_output_tokens").GetInt32());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("json_object", root.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.Equal("function", root.GetProperty("tools")[0].GetProperty("type").GetString());
        Assert.Equal("get_weather", root.GetProperty("tools")[0].GetProperty("name").GetString());
        Assert.Equal("auto", root.GetProperty("tool_choice").GetString());
    }

    [Fact]
    public async Task ResponseAsync_DeserializesOutputItemsAndUsage()
    {
        const string responseJson =
            """
            {
              "id": "resp_123",
              "object": "response",
              "created_at": 1753000000,
              "status": "completed",
              "model": "deepseek-v4-flash",
              "output": [
                {
                  "type": "reasoning",
                  "id": "rs_1",
                  "status": "completed",
                  "content": [{ "type": "reasoning_text", "text": "I should answer." }]
                },
                {
                  "type": "message",
                  "id": "msg_1",
                  "status": "completed",
                  "role": "assistant",
                  "content": [{ "type": "output_text", "text": "Hello!" }]
                },
                {
                  "type": "function_call",
                  "id": "fc_1",
                  "status": "completed",
                  "call_id": "call_1",
                  "name": "get_weather",
                  "arguments": "{\"city\":\"Shanghai\"}"
                }
              ],
              "usage": {
                "input_tokens": 22,
                "input_tokens_details": { "cached_tokens": 4 },
                "output_tokens": 29,
                "output_tokens_details": { "reasoning_tokens": 27 },
                "total_tokens": 51
              }
            }
            """;
        var handler = new TestHttpMessageHandler(
            _ => JsonResponse(responseJson)
        );
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        var client = new DeepSeekClient(httpClient);

        var result = await client.ResponseAsync(
            new ResponseRequest { Input = "Hi" },
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal("resp_123", result!.Id);
        Assert.Equal("completed", result.Status);
        Assert.Equal(3, result.Output.Count);
        Assert.Equal("reasoning_text", result.Output[0].Content![0].Type);
        Assert.Equal("Hello!", result.Output[1].Content![0].Text);
        Assert.Equal("call_1", result.Output[2].CallId);
        Assert.Equal(4, result.Usage!.InputTokensDetails!.CachedTokens);
        Assert.Equal(27, result.Usage.OutputTokensDetails!.ReasoningTokens);
        Assert.Equal("responses", handler.Request!.RequestUri!.AbsolutePath.Trim('/'));
        Assert.Equal("false", JsonDocument.Parse(handler.RequestBody!).RootElement.GetProperty("stream").GetRawText());
    }

    [Fact]
    public async Task ResponseStreamAsync_ParsesSemanticSseEvents()
    {
        const string stream =
            """
            event: response.created
            data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_1","object":"response","status":"in_progress","model":"deepseek-v4-flash","output":[]}}

            event: response.reasoning_text.delta
            data: {"type":"response.reasoning_text.delta","sequence_number":1,"item_id":"rs_1","delta":"Think"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_1","delta":"Hello"}

            event: response.completed
            data: {"type":"response.completed","sequence_number":3,"response":{"id":"resp_1","object":"response","status":"completed","model":"deepseek-v4-flash","output":[],"usage":{"input_tokens":1,"output_tokens":2,"total_tokens":3}}}

            """;
        var handler = new TestHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream"),
            }
        );
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        var client = new DeepSeekClient(httpClient);

        var events = new List<ResponseStreamEvent>();
        await foreach (
            var responseEvent in client.ResponseStreamAsync(
                new ResponseRequest { Input = "Hi" },
                CancellationToken.None
            )
        )
        {
            events.Add(responseEvent);
        }

        Assert.Equal(4, events.Count);
        Assert.Equal("response.created", events[0].Type);
        Assert.Equal("response.created", events[0].EventName);
        Assert.Equal("Think", events[1].Delta);
        Assert.Equal("Hello", events[2].Delta);
        Assert.Equal("response.completed", events[3].EventName);
        Assert.Equal(3, events[3].Response!.Usage!.TotalTokens);
    }

    [Fact]
    public async Task ResponsesChatClient_MapsResponseToMicrosoftExtensionsAI()
    {
        const string responseJson =
            """
            {
              "id": "resp_123",
              "object": "response",
              "created_at": 1753000000,
              "status": "completed",
              "model": "deepseek-v4-flash",
              "output": [
                { "type": "reasoning", "content": [{ "type": "reasoning_text", "text": "Reason" }] },
                { "type": "message", "content": [{ "type": "output_text", "text": "Answer" }] },
                { "type": "function_call", "call_id": "call_1", "name": "get_weather", "arguments": "{\"city\":\"Shanghai\"}" }
              ],
              "usage": { "input_tokens": 5, "output_tokens": 7, "total_tokens": 12 }
            }
            """;
        var handler = new TestHttpMessageHandler(_ => JsonResponse(responseJson));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var chatClient = new DeepSeekResponsesChatClient(httpClient);

        var result = await chatClient.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Hi")],
            cancellationToken: CancellationToken.None
        );

        var message = Assert.Single(result.Messages);
        Assert.Equal("Answer", message.Contents.OfType<TextContent>().Single().Text);
        Assert.Equal("Reason", message.Contents.OfType<TextReasoningContent>().Single().Text);
        var functionCall = message.Contents.OfType<FunctionCallContent>().Single();
        Assert.Equal("call_1", functionCall.CallId);
        Assert.Equal("get_weather", functionCall.Name);
        Assert.Equal("resp_123", result.ResponseId);
        Assert.Equal(12, result.Usage!.TotalTokenCount);
        Assert.Equal(ChatFinishReason.Stop, result.FinishReason);
    }

    [Fact]
    public async Task ResponsesChatClient_MapsStreamingTextReasoningAndCompletion()
    {
        const string stream =
            """
            event: response.created
            data: {"type":"response.created","sequence_number":0,"response":{"id":"resp_1","object":"response","status":"in_progress","model":"deepseek-v4-flash","output":[]}}

            event: response.reasoning_text.delta
            data: {"type":"response.reasoning_text.delta","sequence_number":1,"item_id":"rs_1","delta":"Reason"}

            event: response.output_text.delta
            data: {"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_1","delta":"Answer"}

            event: response.completed
            data: {"type":"response.completed","sequence_number":3,"response":{"id":"resp_1","object":"response","status":"completed","model":"deepseek-v4-flash","output":[]}}

            """;
        var handler = new TestHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream"),
            }
        );
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var chatClient = new DeepSeekResponsesChatClient(httpClient);

        var updates = new List<ChatResponseUpdate>();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Hi")],
                cancellationToken: CancellationToken.None
            )
        )
        {
            updates.Add(update);
        }

        Assert.Equal(3, updates.Count);
        Assert.Equal("Reason", Assert.IsType<TextReasoningContent>(Assert.Single(updates[0].Contents)).Text);
        Assert.Equal("Answer", Assert.IsType<TextContent>(Assert.Single(updates[1].Contents)).Text);
        Assert.Equal(ChatFinishReason.Stop, updates[2].FinishReason);
        Assert.Equal("resp_1", updates[2].ResponseId);
    }

    [Fact]
    public async Task ResponsesChatClient_DoesNotDuplicateStreamingFunctionCall()
    {
        const string stream =
            """
            event: response.output_item.added
            data: {"type":"response.output_item.added","sequence_number":0,"item":{"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_weather","arguments":""}}

            event: response.function_call_arguments.delta
            data: {"type":"response.function_call_arguments.delta","sequence_number":1,"item_id":"fc_1","delta":"{\"city\":\"Shanghai\"}"}

            event: response.function_call_arguments.done
            data: {"type":"response.function_call_arguments.done","sequence_number":2,"item_id":"fc_1","call_id":"call_1","name":"get_weather","arguments":"{\"city\":\"Shanghai\"}"}

            event: response.output_item.done
            data: {"type":"response.output_item.done","sequence_number":3,"item":{"type":"function_call","id":"fc_1","call_id":"call_1","name":"get_weather","arguments":"{\"city\":\"Shanghai\"}"}}

            event: response.completed
            data: {"type":"response.completed","sequence_number":4,"response":{"id":"resp_1","object":"response","status":"completed","model":"deepseek-v4-flash","output":[]}}

            """;
        var handler = new TestHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(stream, Encoding.UTF8, "text/event-stream"),
            }
        );
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var chatClient = new DeepSeekResponsesChatClient(httpClient);

        var updates = new List<ChatResponseUpdate>();
        await foreach (
            var update in chatClient.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Call get_weather")],
                cancellationToken: CancellationToken.None
            )
        )
        {
            updates.Add(update);
        }

        var functionCalls = updates
            .SelectMany(update => update.Contents)
            .OfType<FunctionCallContent>()
            .ToArray();

        var functionCall = Assert.Single(functionCalls);
        Assert.Equal("call_1", functionCall.CallId);
        Assert.Equal("get_weather", functionCall.Name);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class TestHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory
    ) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
