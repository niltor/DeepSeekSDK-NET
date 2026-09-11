using System.Net;
using System.Text;
using System.Text.Json;
using DeepSeek.Core;
using DeepSeek.Core.Adapters;
using DeepSeek.Core.Models;
using Microsoft.Extensions.AI;
using Xunit;

namespace DeepSeek.IntegrationTests;

public sealed class VisionAndFilesApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new DeepSeekClient(new HttpClient())
        .JsonSerializerOptions;

    [Fact]
    public void ChatRequest_SerializesVisionContentBlocks()
    {
        var request = new ChatRequest
        {
            Model = DeepSeekModels.FlashVisionExperimental,
            Messages =
            [
                Message.NewUserMessage(
                [
                    ChatMessageContentPart.CreateTextPart("What is in this image?"),
                    ChatMessageContentPart.CreateImageUrlPart(
                        "data:image/png;base64,AA==",
                        ImageDetailTypes.Low
                    ),
                    ChatMessageContentPart.CreateFilePart("file-api-image"),
                    ChatMessageContentPart.CreateFileDataPart(
                        "data:image/png;base64,AA==",
                        "image.png"
                    ),
                ]),
            ],
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        var message = document.RootElement.GetProperty("messages")[0];
        var content = message.GetProperty("content");

        Assert.Equal(4, content.GetArrayLength());
        Assert.Equal("text", content[0].GetProperty("type").GetString());
        Assert.Equal("What is in this image?", content[0].GetProperty("text").GetString());
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        Assert.Equal(
            "data:image/png;base64,AA==",
            content[1].GetProperty("image_url").GetProperty("url").GetString()
        );
        Assert.Equal("low", content[1].GetProperty("image_url").GetProperty("detail").GetString());
        Assert.Equal("file-api-image", content[2].GetProperty("file_id").GetString());
        Assert.Equal("file", content[3].GetProperty("type").GetString());
        Assert.Equal("data:image/png;base64,AA==", content[3].GetProperty("file_data").GetString());
        Assert.Equal("image.png", content[3].GetProperty("filename").GetString());
    }

    [Fact]
    public void Message_DeserializesArrayContentAndKeepsTextCompatibility()
    {
        const string json =
            """
            {
              "role": "assistant",
              "content": [
                { "type": "text", "text": "A description" },
                { "type": "image_url", "image_url": { "url": "https://example.com/out.png" } }
              ]
            }
            """;

        var message = JsonSerializer.Deserialize<Message>(json, JsonOptions);

        Assert.NotNull(message);
        Assert.Equal("assistant", message!.Role);
        Assert.Equal("A description", message.Content);
        Assert.Equal("A description", message.GetTextContent());
        Assert.Equal(2, message.ContentParts!.Count);
        Assert.Equal("https://example.com/out.png", message.ContentParts[1].ImageUrl!.Url);
    }

    [Fact]
    public async Task FilesApi_UsesMultipartAndSupportsAllFileOperations()
    {
        var handler = new CapturingHandler(request => request.Method.Method switch
        {
            "POST" => JsonResponse(
                """
                {"id":"file-api-1","object":"file","bytes":3,"created_at":1700000000,"filename":"image.png","purpose":"user_data","expires_at":1700003600}
                """
            ),
            "GET" when request.RequestUri?.AbsolutePath.EndsWith("/file-api-1") == true => JsonResponse(
                """
                {"id":"file-api-1","object":"file","bytes":3,"created_at":1700000000,"filename":"image.png","purpose":"user_data"}
                """
            ),
            "GET" => JsonResponse(
                """
                {"object":"list","data":[{"id":"file-api-1","object":"file","bytes":3,"created_at":1700000000,"filename":"image.png","purpose":"user_data"}],"first_id":"file-api-1","last_id":"file-api-1","has_more":false}
                """
            ),
            "DELETE" => JsonResponse(
                """
                {"id":"file-api-1","object":"file","deleted":true}
                """
            ),
            _ => new HttpResponseMessage(HttpStatusCode.MethodNotAllowed),
        });
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        var client = new DeepSeekClient(httpClient);

        using var source = new MemoryStream([1, 2, 3]);
        var uploaded = await client.CreateFileAsync(
            source,
            "image.png",
            new FileUploadOptions
            {
                ContentType = "image/png",
                ExpiresAfterSeconds = 3600,
            }
        );
        var listed = await client.ListFilesAsync(
            new FileListOptions
            {
                After = "file-api-0",
                Limit = 20,
                Order = FileListOrderTypes.Descending,
                Purpose = FilePurposes.UserData,
            }
        );
        var retrieved = await client.RetrieveFileAsync("file-api-1");
        var deleted = await client.DeleteFileAsync("file-api-1");

        Assert.Equal("file-api-1", uploaded!.Id);
        Assert.Equal("image.png", uploaded.Filename);
        Assert.Single(listed!.Data);
        Assert.Equal("file-api-1", retrieved!.Id);
        Assert.True(deleted!.Deleted);
        Assert.True(source.CanRead);

        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal("https://api.deepseek.com/files/file-api-1", handler.LastUri!.ToString());
        var upload = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, upload.Method);
        Assert.Equal("multipart/form-data", upload.ContentType);
        Assert.True(
            upload.Body.Contains("name=\"purpose\"", StringComparison.Ordinal)
                || upload.Body.Contains("name=purpose", StringComparison.Ordinal)
        );
        Assert.Contains("user_data", upload.Body);
        Assert.True(
            upload.Body.Contains("name=\"expires_after[anchor]\"", StringComparison.Ordinal)
                || upload.Body.Contains("name=expires_after[anchor]", StringComparison.Ordinal)
        );
        Assert.Contains("created_at", upload.Body);
        Assert.True(
            upload.Body.Contains("name=\"expires_after[seconds]\"", StringComparison.Ordinal)
                || upload.Body.Contains("name=expires_after[seconds]", StringComparison.Ordinal)
        );
        Assert.Contains("3600", upload.Body);
        Assert.True(
            upload.Body.Contains("filename=\"image.png\"", StringComparison.Ordinal)
                || upload.Body.Contains("filename=image.png", StringComparison.Ordinal)
        );

        var listRequest = handler.Requests[1];
        Assert.NotNull(listRequest.Uri);
        var listQuery = listRequest.Uri!.Query;
        Assert.Contains("after=file-api-0", listQuery);
        Assert.Contains("limit=20", listQuery);
        Assert.Contains("order=desc", listQuery);
        Assert.Contains("purpose=user_data", listQuery);
    }

    [Fact]
    public async Task FilesApi_RejectsUnsupportedPurposeBeforeSending()
    {
        var handler = new CapturingHandler(_ => JsonResponse("{}"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        var client = new DeepSeekClient(httpClient);

        using var source = new MemoryStream([1, 2, 3]);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.CreateFileAsync(
                source,
                "image.png",
                new FileUploadOptions { Purpose = "assistants" }
            )
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListFilesAsync(new FileListOptions { Purpose = "assistants" })
        );

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ChatAdapter_MapsMicrosoftExtensionsAIImageContent()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            {"id":"chat-1","object":"chat.completion","created":1700000000,"model":"deepseek-v4-flash-vision-exp","choices":[{"index":0,"message":{"role":"assistant","content":[{"type":"text","text":"ok"},{"type":"image_url","image_url":{"url":"https://example.com/result.png"}},{"type":"file","file_id":"file-api-1"}]},"finish_reason":"stop"}]}
            """
        ));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var chatClient = new DeepSeekChatClient(new DeepSeekClient(httpClient));

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(
                    ChatRole.User,
                    [
                        new TextContent("Describe this image."),
                        new DataContent(new byte[] { 1, 2, 3 }, "image/png"),
                        new HostedFileContent("file-api-image") { MediaType = "image/png" },
                    ]
                ),
            ],
            new ChatOptions { ModelId = DeepSeekModels.FlashVisionExperimental }
        );

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        var content = document.RootElement.GetProperty("messages")[0].GetProperty("content");
        Assert.Equal("image_url", content[1].GetProperty("type").GetString());
        Assert.Equal(
            "data:image/png;base64,AQID",
            content[1].GetProperty("image_url").GetProperty("url").GetString()
        );
        Assert.Contains(
            response.Messages[0].Contents,
            item => item is HostedFileContent file && file.FileId == "file-api-1"
        );
    }

    [Fact]
    public async Task ResponsesChatAdapter_MapsMicrosoftExtensionsAIImageContent()
    {
        var handler = new CapturingHandler(_ => JsonResponse(
            """
            {"id":"response-1","object":"response","created_at":1700000000,"status":"completed","model":"deepseek-v4-flash-vision-exp","output":[{"type":"message","role":"assistant","content":[{"type":"output_text","text":"ok"},{"type":"input_image","image_url":"https://example.com/result.png"},{"type":"input_image","file_id":"file-api-image"}]}]}
            """
        ));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var chatClient = new DeepSeekResponsesChatClient(new DeepSeekClient(httpClient));

        var response = await chatClient.GetResponseAsync(
            [
                new ChatMessage(
                    ChatRole.User,
                    [
                        new TextContent("Describe this image."),
                        new UriContent("https://example.com/image%20one.png?caption=a%20b", "image/png"),
                        new HostedFileContent("file-api-image") { MediaType = "image/png" },
                    ]
                ),
            ],
            new ChatOptions { ModelId = DeepSeekModels.FlashVisionExperimental }
        );

        using var document = JsonDocument.Parse(handler.Requests[0].Body);
        var content = document.RootElement.GetProperty("input")[0].GetProperty("content");
        Assert.Equal("input_text", content[0].GetProperty("type").GetString());
        Assert.Equal("input_image", content[1].GetProperty("type").GetString());
        Assert.Equal(
            "https://example.com/image%20one.png?caption=a%20b",
            content[1].GetProperty("image_url").GetString()
        );
        Assert.Equal("input_image", content[2].GetProperty("type").GetString());
        Assert.Equal("file-api-image", content[2].GetProperty("file_id").GetString());
        Assert.Contains(
            response.Messages[0].Contents,
            item => item is HostedFileContent file && file.FileId == "file-api-image"
        );
    }

    [Fact]
    public void ResponseRequest_SerializesInputImageAndFileParts()
    {
        var request = new ResponseRequest
        {
            Model = DeepSeekModels.FlashVisionExperimental,
            Input = JsonSerializer.SerializeToNode(
                new[]
                {
                    ResponseInputItem.NewUserMessage(
                    [
                        ResponseContentPart.CreateInputTextPart("Describe this image."),
                        ResponseContentPart.CreateInputImagePart(
                            "https://example.com/image.png",
                            ImageDetailTypes.Low
                        ),
                        ResponseContentPart.CreateInputFilePart("file-api-image"),
                    ]),
                },
                JsonOptions
            ),
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        var content = document.RootElement.GetProperty("input")[0].GetProperty("content");

        Assert.Equal("input_text", content[0].GetProperty("type").GetString());
        Assert.Equal("input_image", content[1].GetProperty("type").GetString());
        Assert.Equal("https://example.com/image.png", content[1].GetProperty("image_url").GetString());
        Assert.Equal("low", content[1].GetProperty("detail").GetString());
        Assert.Equal("file-api-image", content[2].GetProperty("file_id").GetString());
    }

    [Fact]
    public void ResponseFunctionCallOutput_SerializesMultimodalOutput()
    {
        var item = ResponseInputItem.NewFunctionCallOutput(
            "call-1",
            [
                ResponseContentPart.CreateInputTextPart("tool result"),
                ResponseContentPart.CreateInputImagePart("https://example.com/result.png"),
            ]
        );

        var json = JsonSerializer.Serialize(item, JsonOptions);
        using var document = JsonDocument.Parse(json);
        var output = document.RootElement.GetProperty("output");

        Assert.Equal(JsonValueKind.Array, output.ValueKind);
        Assert.Equal("input_image", output[1].GetProperty("type").GetString());

        var roundTrip = JsonSerializer.Deserialize<ResponseInputItem>(json, JsonOptions);
        Assert.NotNull(roundTrip);
        Assert.Equal(2, roundTrip!.OutputContentParts!.Count);
        Assert.Equal("tool result", roundTrip.OutputContentParts[0].Text);

        var textOutputJson = JsonSerializer.Serialize(
            ResponseInputItem.NewFunctionCallOutput("call-2", "plain result"),
            JsonOptions
        );
        using var textOutputDocument = JsonDocument.Parse(textOutputJson);
        Assert.Equal(
            "plain result",
            textOutputDocument.RootElement.GetProperty("output").GetString()
        );
        var textRoundTrip = JsonSerializer.Deserialize<ResponseInputItem>(textOutputJson, JsonOptions);
        Assert.Equal("plain result", textRoundTrip!.Output);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class CapturingHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory
    ) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];
        public HttpMethod? LastMethod => Requests.LastOrDefault()?.Method;
        public Uri? LastUri => Requests.LastOrDefault()?.Uri;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(
                new CapturedRequest
                {
                    Method = request.Method,
                    Uri = request.RequestUri,
                    Body = body,
                    ContentType = request.Content?.Headers.ContentType?.MediaType,
                }
            );
            return responseFactory(request);
        }
    }

    private sealed class CapturedRequest
    {
        public HttpMethod Method { get; init; } = HttpMethod.Get;
        public Uri? Uri { get; init; }
        public string Body { get; init; } = string.Empty;
        public string? ContentType { get; init; }
    }
}
