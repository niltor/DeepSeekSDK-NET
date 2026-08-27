using DeepSeek.Core.Models;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Globalization;

namespace DeepSeek.Core;

public class DeepSeekClient : IDisposable
{
    /// <summary>
    /// base address
    /// </summary>
    public readonly string BaseAddress = "https://api.deepseek.com/";
    public readonly string BetaBaseAddress = "https://api.deepseek.com/beta/";
    /// <summary>
    /// chat endpoint
    /// </summary>
    public string ChatEndpoint { get; private set; } = "chat/completions";
    /// <summary>
    /// OpenAI-compatible Responses API endpoint.
    /// </summary>
    public string ResponsesEndpoint { get; private set; } = "responses";
    public string CompletionEndpoint { get; private set; } = "completions";
    public readonly string UserBalanceEndpoint = "user/balance";
    /// <summary>
    /// OpenAI-compatible Files API endpoint.
    /// </summary>
    public string FilesEndpoint { get; private set; } = "files";

    /// <summary>
    /// list models endpoint
    /// </summary>
    public readonly string ModelsEndpoint = "models";

    /// <summary>
    ///  done sign
    /// </summary>
    private const string StreamDoneSign = "[DONE]";

    protected readonly HttpClient Http;
    private readonly bool _ownsHttpClient;
    public JsonSerializerOptions JsonSerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = SourceGenerationContext.Default,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    public string? ErrorMsg { get; private set; }

    /// <summary>
    /// for dependency injection
    /// </summary>
    /// <param name="httpClient"></param>
    public DeepSeekClient(HttpClient httpClient)
    {
        Http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = false;
        if (Http.BaseAddress is null)
        {
            Http.BaseAddress = new Uri(BaseAddress);
        }
    }

    public DeepSeekClient(HttpClient http, string apiKey)
    {
        Http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttpClient = false;
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (Http.BaseAddress is null)
        {
            Http.BaseAddress = new Uri(BaseAddress);
        }
        if (Http.DefaultRequestHeaders.Authorization is null)
        {
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
        }
    }

    public DeepSeekClient(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        Http = new HttpClient()
        {
            BaseAddress = new Uri(BaseAddress),
            Timeout = TimeSpan.FromSeconds(120),
        };
        _ownsHttpClient = true;
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

    public void SetResponsesEndpoint(string endpoint)
    {
        ResponsesEndpoint = endpoint;
    }

    public void SetCompletionEndpoint(string endpoint)
    {
        CompletionEndpoint = endpoint;
    }

    public void SetFilesEndpoint(string endpoint)
    {
        FilesEndpoint = endpoint;
    }

    public async Task<ModelResponse?> ListModelsAsync(CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(ModelsEndpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync();
            ErrorMsg = response.StatusCode.ToString() + res;
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ModelResponse>(content, JsonSerializerOptions);
    }

    /// <summary>
    /// Uploads an image to the OpenAI-compatible Files API.
    /// The caller retains ownership of <paramref name="file"/> and must dispose it.
    /// </summary>
    public async Task<FileObject?> CreateFileAsync(
        Stream file,
        string fileName,
        FileUploadOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!file.CanRead)
        {
            throw new ArgumentException("The file stream must be readable.", nameof(file));
        }

        var uploadOptions = options ?? new FileUploadOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadOptions.Purpose);
        ValidateFilePurpose(uploadOptions.Purpose, nameof(FileUploadOptions.Purpose));
        ValidateExpiration(uploadOptions.ExpiresAfterSeconds);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(uploadOptions.Purpose), "purpose");

        // StreamContent disposes the wrapped stream when the multipart content is
        // disposed. Keep the caller-owned stream open; the overloads that create
        // their own streams dispose those streams themselves after this call.
        using var fileContent = new StreamContent(new NonDisposingStream(file));
        if (!string.IsNullOrWhiteSpace(uploadOptions.ContentType))
        {
            fileContent.Headers.TryAddWithoutValidation(
                "Content-Type",
                uploadOptions.ContentType
            );
        }
        form.Add(fileContent, "file", fileName);

        if (uploadOptions.ExpiresAfterSeconds.HasValue)
        {
            form.Add(new StringContent("created_at"), "expires_after[anchor]");
            form.Add(
                new StringContent(
                    uploadOptions.ExpiresAfterSeconds.Value.ToString(
                        CultureInfo.InvariantCulture
                    )
                ),
                "expires_after[seconds]"
            );
        }

        using var response = await Http.PostAsync(FilesEndpoint, form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            ErrorMsg = response.StatusCode + error;
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            ErrorMsg = "empty response";
            return null;
        }

        return JsonSerializer.Deserialize<FileObject>(content, JsonSerializerOptions);
    }

    public Task<FileObject?> CreateFileAsync(
        Stream file,
        string fileName,
        CancellationToken cancellationToken
    ) => CreateFileAsync(file, fileName, null, cancellationToken);

    /// <summary>
    /// Uploads an image represented by a byte array to the Files API.
    /// </summary>
    public async Task<FileObject?> CreateFileAsync(
        byte[] file,
        string fileName,
        FileUploadOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);
        using var stream = new MemoryStream(file, writable: false);
        return await CreateFileAsync(
            stream,
            fileName,
            options,
            cancellationToken
        );
    }

    /// <summary>
    /// Uploads a local file to the Files API.
    /// </summary>
    public async Task<FileObject?> CreateFileAsync(
        FileInfo file,
        FileUploadOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(file);
        using var stream = file.OpenRead();
        return await CreateFileAsync(stream, file.Name, options, cancellationToken);
    }

    /// <summary>
    /// Alias using the terminology from the Files API guide.
    /// </summary>
    public Task<FileObject?> UploadFileAsync(
        Stream file,
        string fileName,
        FileUploadOptions? options = null,
        CancellationToken cancellationToken = default
    ) => CreateFileAsync(file, fileName, options, cancellationToken);

    public Task<FileObject?> UploadFileAsync(
        Stream file,
        string fileName,
        CancellationToken cancellationToken
    ) => CreateFileAsync(file, fileName, null, cancellationToken);

    public Task<FileObject?> UploadFileAsync(
        byte[] file,
        string fileName,
        FileUploadOptions? options = null,
        CancellationToken cancellationToken = default
    ) => CreateFileAsync(file, fileName, options, cancellationToken);

    /// <summary>
    /// Lists the files belonging to the current API key.
    /// </summary>
    public async Task<FileListResponse?> ListFilesAsync(
        FileListOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        ValidateFileListOptions(options);
        var endpoint = BuildFileListEndpoint(options);
        using var response = await Http.GetAsync(endpoint, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            ErrorMsg = response.StatusCode + error;
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            ErrorMsg = "empty response";
            return null;
        }

        return JsonSerializer.Deserialize<FileListResponse>(content, JsonSerializerOptions);
    }

    public Task<FileListResponse?> ListFilesAsync(CancellationToken cancellationToken) =>
        ListFilesAsync(null, cancellationToken);

    /// <summary>
    /// Gets metadata for a single Files API file.
    /// </summary>
    public async Task<FileObject?> RetrieveFileAsync(
        string fileId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        using var response = await Http.GetAsync(
            $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}",
            cancellationToken
        );
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            ErrorMsg = response.StatusCode + error;
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            ErrorMsg = "empty response";
            return null;
        }

        return JsonSerializer.Deserialize<FileObject>(content, JsonSerializerOptions);
    }

    /// <summary>
    /// Alias for <see cref="RetrieveFileAsync"/>.
    /// </summary>
    public Task<FileObject?> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken = default
    ) => RetrieveFileAsync(fileId, cancellationToken);

    /// <summary>
    /// Deletes a file belonging to the current API key.
    /// </summary>
    public async Task<FileDeleteResponse?> DeleteFileAsync(
        string fileId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}"
        );
        using var response = await Http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            ErrorMsg = response.StatusCode + error;
            return null;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            ErrorMsg = "empty response";
            return null;
        }

        return JsonSerializer.Deserialize<FileDeleteResponse>(content, JsonSerializerOptions);
    }

    private static void ValidateExpiration(int? expiresAfterSeconds)
    {
        if (
            expiresAfterSeconds is not null
            && (expiresAfterSeconds < 3600 || expiresAfterSeconds > 2_592_000)
        )
        {
            throw new ArgumentOutOfRangeException(
                nameof(expiresAfterSeconds),
                "DeepSeek accepts file expiration values from 3600 to 2592000 seconds."
            );
        }
    }

    private static void ValidateFileListOptions(FileListOptions? options)
    {
        if (options?.Purpose is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Purpose);
            ValidateFilePurpose(options.Purpose, nameof(FileListOptions.Purpose));
        }

        if (options?.Limit is not null && (options.Limit < 1 || options.Limit > 1000))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Limit),
                "DeepSeek accepts file list limits from 1 to 1000."
            );
        }

        if (
            options?.Order is not null
            && options.Order is not FileListOrderTypes.Ascending
            && options.Order is not FileListOrderTypes.Descending
        )
        {
            throw new ArgumentException(
                "File list order must be 'asc' or 'desc'.",
                nameof(options.Order)
            );
        }
    }

    private static void ValidateFilePurpose(string purpose, string parameterName)
    {
        if (!string.Equals(purpose, FilePurposes.UserData, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"DeepSeek Files API only supports the '{FilePurposes.UserData}' purpose.",
                parameterName
            );
        }
    }

    private string BuildFileListEndpoint(FileListOptions? options)
    {
        if (options is null)
        {
            return FilesEndpoint;
        }

        var query = new List<string>();
        AddQueryParameter(query, "after", options.After);
        AddQueryParameter(query, "limit", options.Limit?.ToString(CultureInfo.InvariantCulture));
        AddQueryParameter(query, "order", options.Order);
        AddQueryParameter(query, "purpose", options.Purpose);
        return query.Count == 0
            ? FilesEndpoint
            : $"{FilesEndpoint}?{string.Join("&", query)}";
    }

    private static void AddQueryParameter(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            query.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }

    /// <summary>
    /// Prevents disposal of a caller-owned stream while still allowing
    /// <see cref="StreamContent"/> to dispose its wrapper normally.
    /// </summary>
    private sealed class NonDisposingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => inner.Read(buffer);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            inner.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken
        ) => inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => inner.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            // Intentionally leave inner open because ownership belongs to the caller.
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// chat
    /// </summary>
    /// <param name="request"></param>
    public async Task<ChatResponse?> ChatAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        request.Stream = false;
        var content = new StringContent(JsonSerializer.Serialize(request, typeof(ChatRequest), JsonSerializerOptions), Encoding.UTF8, "application/json");

        using var response = await Http.PostAsync(ChatEndpoint, content, cancellationToken);
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
    /// Creates a non-streaming response using the OpenAI-compatible Responses API.
    /// </summary>
    public async Task<ResponseResult?> ResponseAsync(
        ResponseRequest request,
        CancellationToken cancellationToken
    )
    {
        request.Stream = false;
        var content = new StringContent(
            JsonSerializer.Serialize(request, typeof(ResponseRequest), JsonSerializerOptions),
            Encoding.UTF8,
            "application/json"
        );

        using var response = await Http.PostAsync(ResponsesEndpoint, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync(cancellationToken);
            ErrorMsg = response.StatusCode + res;
            return null;
        }

        var resContent = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(resContent))
        {
            ErrorMsg = "empty response";
            return null;
        }

        return JsonSerializer.Deserialize<ResponseResult>(resContent, JsonSerializerOptions);
    }

    /// <summary>
    /// Streams semantic SSE events from the OpenAI-compatible Responses API.
    /// </summary>
    public async IAsyncEnumerable<ResponseStreamEvent> ResponseStreamAsync(
        ResponseRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        request.Stream = true;
        var content = new StringContent(
            JsonSerializer.Serialize(request, typeof(ResponseRequest), JsonSerializerOptions),
            Encoding.UTF8,
            "application/json"
        );
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, ResponsesEndpoint)
        {
            Content = content,
        };

        using var response = await Http.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var res = await response.Content.ReadAsStringAsync(cancellationToken);
            ErrorMsg = response.StatusCode + res;
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        string? eventName = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                eventName = null;
                continue;
            }

            if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
            {
                eventName = line["event:".Length..].Trim();
                continue;
            }

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var json = line["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            if (json == StreamDoneSign)
            {
                yield break;
            }

            var streamEvent = JsonSerializer.Deserialize<ResponseStreamEvent>(json, JsonSerializerOptions);
            if (streamEvent is null)
            {
                continue;
            }

            streamEvent.EventName = eventName;
            if (string.IsNullOrWhiteSpace(streamEvent.Type))
            {
                streamEvent.Type = eventName ?? string.Empty;
            }

            yield return streamEvent;

            if (
                streamEvent.Type is ResponseEventTypes.Completed
                    or ResponseEventTypes.Incomplete
                    or ResponseEventTypes.Failed
            )
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Alias matching the terminology used by the OpenAI .NET SDK.
    /// </summary>
    public Task<ResponseResult?> CreateResponseAsync(
        ResponseRequest request,
        CancellationToken cancellationToken
    ) => ResponseAsync(request, cancellationToken);

    /// <summary>
    /// Alias matching the terminology used by the OpenAI .NET SDK.
    /// </summary>
    public IAsyncEnumerable<ResponseStreamEvent> CreateResponseStreamingAsync(
        ResponseRequest request,
        CancellationToken cancellationToken
    ) => ResponseStreamAsync(request, cancellationToken);

    /// <summary>
    /// streaming output
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

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
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
    /// return raw response string
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async IAsyncEnumerable<string?>? ChatStreamWithStringAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
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

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                yield return line;
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
            endpoint = "beta/" + CompletionEndpoint;
        }
        await Task.Delay(100);
        using var response = await Http.PostAsync(endpoint, content, cancellationToken);
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
            endpoint = "beta/" + CompletionEndpoint;
        }
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content,
        };

        using var response = await Http.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (line.StartsWith("data: "))
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
        using var response = await Http.GetAsync(UserBalanceEndpoint, cancellationToken);
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

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            Http.Dispose();
        }
    }
}
