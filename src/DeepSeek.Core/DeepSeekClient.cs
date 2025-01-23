using System.Text;
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
    public readonly string BetaBaseAddress = "https://api.deepseek.com/beta";
    /// <summary>
    /// chat endpoint
    /// </summary>
    public readonly string ChatEndpoint = "/chat/completions";

    public readonly string CompletionEndpoint = "/completions";
    public readonly string UserBalanceEndpoint = "/user/balance";

    /// <summary>
    /// list models endpoint
    /// </summary>
    public readonly string ModelsEndpoint = "/models";

    /// <summary>
    ///  done sign
    /// </summary>
    private const string StreamDoneSign = "data: [DONE]";

    protected readonly HttpClient Http;
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
        if (http.BaseAddress is null)
        {
            http.BaseAddress = new Uri(BaseAddress);
        }
        if (Http.DefaultRequestHeaders.Authorization is null)
        {
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        }
    }

    public DeepSeekClient(string apiKey)
    {
        Http = new HttpClient()
        {
            BaseAddress = new Uri(BaseAddress),
            Timeout = TimeSpan.FromSeconds(120),
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
    /// chat
    /// </summary>
    /// <param name="request"></param>
    public async Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");

        var response = await Http.PostAsync(ChatEndpoint, content, cancellationToken);
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

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint)
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
                    if (line == StreamDoneSign)
                    {
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(line)) { continue; }

                    var lineData = line.Replace("data:", "").Trim();
                    var chatResponse = JsonSerializer.Deserialize<ChatResponse>(lineData, JsonSerializerOptions);

                    var choice = chatResponse?.Choices.FirstOrDefault();
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
            ErrorMsg = response.StatusCode.ToString() + res;
            return null;
        }
    }


    /// <summary>
    /// Completions
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public async Task<ChatResponse?> CompletionsAsync(CompletionRequest request, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");

        string endpoint = CompletionEndpoint;
        if (Http.BaseAddress?.OriginalString == BaseAddress)
        {
            endpoint = "/beta" + CompletionEndpoint;
        }
        await Task.Delay(100);
        var response = await Http.PostAsync(endpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            return null;
        }
        return JsonSerializer.Deserialize<ChatResponse>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions);
    }

    /// <summary>
    /// Completions
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IAsyncEnumerable<Choice>?> CompletionsStreamAsync(CompletionRequest request, CancellationToken cancellationToken)
    {
        request.Stream = true;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");
        string endpoint = CompletionEndpoint;
        if (Http.BaseAddress?.OriginalString == BaseAddress)
        {
            endpoint = "/beta" + CompletionEndpoint;
        }
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
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
                    if (line == StreamDoneSign)
                    {
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(line)) { continue; }

                    var lineData = line.Replace("data:", "").Trim();
                    var chatResponse = JsonSerializer.Deserialize<ChatResponse>(lineData, JsonSerializerOptions);

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

    /// <summary>
    /// get user balance
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<UserResponse?> GetUserBalanceAsync(CancellationToken cancellationToken)
    {
        var response = await Http.GetAsync(UserBalanceEndpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<UserResponse>(await response.Content.ReadAsStringAsync(), JsonSerializerOptions);
    }
}
