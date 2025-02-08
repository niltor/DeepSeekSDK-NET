using System.Runtime.CompilerServices;
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
    public string ChatEndpoint { get; private set; } = "/chat/completions";
    public string CompletionEndpoint { get; private set; } = "/completions";
    public readonly string UserBalanceEndpoint = "/user/balance";

    /// <summary>
    /// list models endpoint
    /// </summary>
    public readonly string ModelsEndpoint = "/models";

    /// <summary>
    ///  done sign
    /// </summary>
    private const string StreamDoneSign = "[DONE]";

    protected readonly HttpClient Http;
    public JsonSerializerOptions JsonSerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string? ErrorMsg { get; private set; }

    /// <summary>
    /// for dependency injection
    /// </summary>
    /// <param name="httpClient"></param>
    public DeepSeekClient(HttpClient httpClient)
    {
        Http = httpClient;
    }

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
    public void SetChatEndpoint(string endpoint)
    {
        ChatEndpoint = endpoint;
    }

    public void SetCompletionEndpoint(string endpoint)
    {
        CompletionEndpoint = endpoint;
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
        var resContent = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(resContent))
        {
            ErrorMsg = "empty response";
            return null;
        }
        return JsonSerializer.Deserialize<ChatResponse>(resContent, JsonSerializerOptions);
    }

    /// <summary>
    /// 流式输出 
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async IAsyncEnumerable<Choice>? ChatStreamAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        request.Stream = true;
        var content = new StringContent(JsonSerializer.Serialize(request, JsonSerializerOptions), Encoding.UTF8, "application/json");

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, ChatEndpoint)
        {
            Content = content,
        };
        using var response = await Http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line != null && line.StartsWith("data: "))
                {
                    var json = line.Substring(6);
                    if (!string.IsNullOrWhiteSpace(json) && json != StreamDoneSign)
                    {
                        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(json, JsonSerializerOptions);
                        var choice = chatResponse?.Choices.FirstOrDefault();
                        if (choice is null)
                        {
                            continue;
                        }
                        yield return choice;
                    }
                }
            }
        }
        else
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            yield break;
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
        var resContent = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(resContent))
        {
            ErrorMsg = "empty response";
            return null;
        }
        return JsonSerializer.Deserialize<ChatResponse>(resContent, JsonSerializerOptions);
    }

    /// <summary>
    /// Completions
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<Choice>? CompletionsStreamAsync(CompletionRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
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
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line != null && line.StartsWith("data: "))
                {
                    var json = line.Substring(6);
                    if (!string.IsNullOrWhiteSpace(json) && json != StreamDoneSign)
                    {
                        var chatResponse = JsonSerializer.Deserialize<ChatResponse>(json, JsonSerializerOptions);
                        var choice = chatResponse?.Choices.FirstOrDefault();
                        if (choice is null)
                        {
                            continue;
                        }
                        yield return choice;
                    }
                }
            }
        }
        else
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            yield break;
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
        if (string.IsNullOrWhiteSpace(content))
        {
            ErrorMsg = "empty response";
            return null;
        }
        return JsonSerializer.Deserialize<UserResponse>(content, JsonSerializerOptions);
    }
}
