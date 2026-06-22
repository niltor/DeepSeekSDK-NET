using System.Text.Json;
using DeepSeek.Core;
using DeepSeek.Core.Models;
using Xunit;

namespace DeepSeek.IntegrationTests;

public class ToolCallsSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new DeepSeekClient(new HttpClient())
        .JsonSerializerOptions;

    [Fact]
    public void ChatResponse_DeserializesToolCallIndexInAssistantMessage()
    {
        const string json =
            """
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "created": 1779560530,
              "model": "deepseek-chat",
              "choices": [
                {
                  "index": 0,
                  "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                      {
                        "index": 1,
                        "id": "call_123",
                        "type": "function",
                        "function": {
                          "name": "GetWeather",
                          "arguments": "{\"city\":\"New York\"}"
                        }
                      }
                    ]
                  },
                  "finish_reason": "tool_calls"
                }
              ]
            }
            """;

        var response = JsonSerializer.Deserialize<ChatResponse>(json, JsonOptions);

        var toolCall = Assert.Single(response!.Choices[0].Message!.ToolCalls!);
        Assert.Equal(1, toolCall.Index);
    }

    [Fact]
    public void ChatResponse_DeserializesToolCallIndexInStreamingDelta()
    {
        const string functionDeltaJson =
            """
            {
              "id": "chatcmpl-2",
              "object": "chat.completion.chunk",
              "created": 1779560530,
              "model": "deepseek-chat",
              "choices": [
                {
                  "index": 0,
                  "delta": {
                    "tool_calls": [
                      {
                        "index": 0,
                        "id": "call_abc",
                        "type": "function",
                        "function": {
                          "name": "WarframeMarketItemManifest",
                          "arguments": ""
                        }
                      }
                    ]
                  },
                  "finish_reason": null
                }
              ]
            }
            """;

        const string argumentsDeltaJson =
            """
            {
              "id": "chatcmpl-2",
              "object": "chat.completion.chunk",
              "created": 1779560530,
              "model": "deepseek-chat",
              "choices": [
                {
                  "index": 0,
                  "delta": {
                    "tool_calls": [
                      {
                        "index": 0,
                        "type": "function",
                        "function": {
                          "arguments": "{\"Name\":\"Braton Vandal\"}"
                        }
                      }
                    ]
                  },
                  "finish_reason": null
                }
              ]
            }
            """;

        var functionDelta = JsonSerializer.Deserialize<ChatResponse>(functionDeltaJson, JsonOptions);
        var argumentsDelta = JsonSerializer.Deserialize<ChatResponse>(argumentsDeltaJson, JsonOptions);

        var firstToolCall = Assert.Single(functionDelta!.Choices[0].Delta!.ToolCalls!);
        var secondToolCall = Assert.Single(argumentsDelta!.Choices[0].Delta!.ToolCalls!);

        Assert.Equal(0, firstToolCall.Index);
        Assert.Equal(firstToolCall.Index, secondToolCall.Index);
    }

    [Fact]
    public void ChatRequest_SerializesThinkingFields_WithoutDeprecatedPenaltyFields()
    {
        var request = new ChatRequest
        {
            Messages = [Message.NewUserMessage("Hi")],
            Model = DeepSeekModels.Pro,
            Thinking = new Thinking { Type = ThinkingTypes.Disabled },
            ReasoningEffort = ReasoningEffortTypes.Max,
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.TryGetProperty("frequency_penalty", out _));
        Assert.False(root.TryGetProperty("presence_penalty", out _));
        Assert.Equal("disabled", root.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("max", root.GetProperty("reasoning_effort").GetString());
    }
}
