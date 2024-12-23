﻿using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading.Channels;
using DeepSeek.Core.Models;

namespace DeepSeek.Core;

public class DeepSeekClient
{
    /// <summary>
    /// 域名
    /// </summary>
    public readonly string BaseAddress = "https://api.deepseek.com";
    /// <summary>
    /// chat endpoint
    /// </summary>
    public readonly string CompletionEndpoint = "/chat/completions";
    /// <summary>
    /// list models endpoint
    /// </summary>
    public readonly string ModelsEndpoint = "/models";

    /// <summary>
    ///  done sign
    /// </summary>
    private const string StreamDoneSign = "[DONE]";

    private readonly HttpClient Http;
    public JsonSerializerOptions JsonSerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string? ErrorMsg { get; set; }

    public DeepSeekClient(HttpClient http, string apiKey)
    {
        Http = http;
        http.BaseAddress = new Uri(BaseAddress);
        Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
    }

    public DeepSeekClient(string apiKey)
    {
        Http = new HttpClient()
        {
            BaseAddress = new Uri(BaseAddress),
            Timeout = TimeSpan.FromSeconds(60),
        };
        Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
    }

    public void SetTimeout(int seconds)
    {
        Http.Timeout = TimeSpan.FromSeconds(seconds);
    }

    public async Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync(ModelsEndpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            return null;
        }
        return JsonSerializer.Deserialize<ModelResponse>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions);
    }

    /// <summary>
    /// completion
    /// </summary>
    /// <param name="request"></param>
    /// <returns>错误代码:https://platform.deepseek.com/docs#error-codes</returns>
    public async Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");

        var response = await Http.PostAsync(CompletionEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            return null;
        }
        return JsonSerializer.Deserialize<ChatResponse>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions);
    }


    /// <summary>
    /// 流式输出 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task<IAsyncEnumerable<Choice>?> ChatStreamAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = true;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, CompletionEndpoint)
        {
            Content = content,
        };
        var response = await Http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync();
            var reader = new StreamReader(stream);

            var channel = Channel.CreateUnbounded<Choice>();
            _ = Task.Run(async () =>
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    line = line?.Replace("data:", "").Trim();
                    if (line == StreamDoneSign)
                    {
                        break;
                    }
                    if (line is null or "") { continue; }

                    var chatResponse = JsonSerializer.Deserialize<ChatResponse>(line, JsonSerializerOptions);

                    var choice = chatResponse?.Choices.First();
                    if (choice is null)
                    {
                        continue;
                    }
                    await channel.Writer.WriteAsync(choice);
                }
                channel.Writer.Complete();
            });

            return channel.Reader.ReadAllAsync();
        }
        else
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = res;
            return null;
        }
    }

}
