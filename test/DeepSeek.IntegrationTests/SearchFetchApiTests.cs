using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeepSeek.Core;
using DeepSeek.Core.Models;
using Xunit;

namespace DeepSeek.IntegrationTests;

public sealed class SearchFetchApiTests
{
    [Fact]
    public async Task SearchAndFetch_UseStandaloneJsonEndpoints()
    {
        var handler = new CapturingHandler(request =>
            request.RequestUri?.AbsolutePath switch
            {
                "/v1/tools/search" => JsonResponse(
                    """
                    {
                      "results": [
                        {
                          "rerank_score": 2.4,
                          "title": "DeepSeek API",
                          "url": "https://example.com/deepseek",
                          "snippet": "A short result",
                          "summary": "A longer result summary",
                          "content": "# DeepSeek API",
                          "site_name": "Example",
                          "site_icon": "https://example.com/favicon.ico",
                          "date": "2026-10-17T09:00:12.045Z",
                          "authority": 3
                        }
                      ]
                    }
                    """
                ),
                "/v1/tools/fetch" => JsonResponse(
                    """
                    {"title":"Fetched page","content":"# Markdown\n\nFetched content."}
                    """
                ),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            }
        );
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var client = new DeepSeekClient(httpClient, "test-key");

        var search = await client.SearchAsync(
            new SearchRequest
            {
                Query = "DeepSeek site:example.com",
                Count = 5,
                OriginalQuestion = "What is the DeepSeek API?",
                Localization = "203.0.113.10",
                NeedContent = true,
            }
        );
        var fetch = await client.FetchAsync(
            new FetchRequest { Url = "https://example.com/deepseek" }
        );

        Assert.NotNull(search);
        var result = Assert.Single(search!.Results);
        Assert.Equal(2.4, result.RerankScore);
        Assert.Equal("DeepSeek API", result.Title);
        Assert.Equal("https://example.com/deepseek", result.Url);
        Assert.Equal("Example", result.SiteName);
        Assert.Equal(3, result.Authority);
        Assert.Equal(
            new DateTimeOffset(2026, 10, 17, 9, 0, 12, TimeSpan.Zero).AddMilliseconds(45),
            result.Date
        );

        Assert.NotNull(fetch);
        Assert.Equal("Fetched page", fetch!.Title);
        Assert.Contains("# Markdown", fetch.Content);

        Assert.Equal(2, handler.Requests.Count);
        var searchRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, searchRequest.Method);
        Assert.Equal("https://api.deepseek.com/v1/tools/search", searchRequest.Uri!.ToString());
        Assert.Equal("Bearer test-key", searchRequest.Authorization);
        Assert.Equal("application/json", searchRequest.ContentType);
        using (var searchBody = JsonDocument.Parse(searchRequest.Body))
        {
            var root = searchBody.RootElement;
            Assert.Equal("DeepSeek site:example.com", root.GetProperty("query").GetString());
            Assert.Equal(5, root.GetProperty("count").GetInt32());
            Assert.Equal(
                "What is the DeepSeek API?",
                root.GetProperty("ori_question").GetString()
            );
            Assert.Equal("203.0.113.10", root.GetProperty("localization").GetString());
            Assert.True(root.GetProperty("need_content").GetBoolean());
        }

        var fetchRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, fetchRequest.Method);
        Assert.Equal("https://api.deepseek.com/v1/tools/fetch", fetchRequest.Uri!.ToString());
        Assert.Equal("Bearer test-key", fetchRequest.Authorization);
        using var fetchBody = JsonDocument.Parse(fetchRequest.Body);
        Assert.Equal(
            "https://example.com/deepseek",
            fetchBody.RootElement.GetProperty("url").GetString()
        );
    }

    [Fact]
    public async Task SearchAndFetch_RejectInvalidRequestsBeforeSending()
    {
        var handler = new CapturingHandler(_ => JsonResponse("{}"));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.deepseek.com/"),
        };
        using var client = new DeepSeekClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SearchAsync(new SearchRequest { Query = "   " })
        );
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SearchAsync(new SearchRequest { Query = "query", Count = 51 })
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.FetchAsync(new FetchRequest { Url = "ftp://example.com/page" })
        );

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(128, false)]
    [InlineData(129, false)]
    [InlineData(128, true)]
    [InlineData(129, true)]
    public async Task Search_ValidatesUnicodeCharacterLimits(int length, bool originalQuestion)
    {
        var handler = new CapturingHandler(_ => JsonResponse("{\"results\":[]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);
        // Each emoji is one Unicode scalar but two UTF-16 code units.
        var text = string.Concat(Enumerable.Repeat("\U0001F600", length));
        var request = new SearchRequest
        {
            Query = originalQuestion ? "DeepSeek" : text,
            OriginalQuestion = originalQuestion ? text : null,
        };

        if (length > 128)
        {
            var error = await Assert.ThrowsAsync<ArgumentException>(() => client.SearchAsync(request));
            Assert.Equal(originalQuestion ? "OriginalQuestion" : "Query", error.ParamName);
            Assert.Empty(handler.Requests);
        }
        else
        {
            var response = await client.SearchAsync(request);
            Assert.NotNull(response);
            Assert.Empty(response!.Results);
            Assert.Single(handler.Requests);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(null)]
    public async Task Search_SerializesCountBoundariesAndOmitsUnsetOptions(int? count)
    {
        var handler = new CapturingHandler(_ => JsonResponse("{\"results\":[]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        Assert.NotNull(await client.SearchAsync(new SearchRequest { Query = "DeepSeek", Count = count }));

        using var body = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(count.HasValue ? 2 : 1, body.RootElement.EnumerateObject().Count());
        if (count.HasValue)
        {
            Assert.Equal(count.Value, body.RootElement.GetProperty("count").GetInt32());
        }
    }

    [Theory]
    [InlineData("https://user:password@example.com/")]
    [InlineData("/relative")]
    [InlineData(" ")]
    public async Task Fetch_RejectsInvalidUrls(string url)
    {
        var handler = new CapturingHandler(_ => JsonResponse("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.FetchAsync(new FetchRequest { Url = url }));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAndFetch_RejectNullRequests(bool search)
    {
        var handler = new CapturingHandler(_ => JsonResponse("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            if (search)
                await client.SearchAsync(null!);
            else
                await client.FetchAsync(null!);
        });
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAndFetch_HandleEmptyResponsesAndCustomEndpoints(bool search)
    {
        var handler = new CapturingHandler(_ => JsonResponse(" "));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);
        client.SetSearchEndpoint("gateway/search");
        client.SetFetchEndpoint("gateway/fetch");

        if (search)
            Assert.Null(await client.SearchAsync(new SearchRequest { Query = "DeepSeek" }));
        else
            Assert.Null(await client.FetchAsync(new FetchRequest { Url = "https://example.com/" }));

        Assert.Equal("empty response", client.ErrorMsg);
        Assert.Equal(search ? "/gateway/search" : "/gateway/fetch", Assert.Single(handler.Requests).Uri!.AbsolutePath);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAndFetch_PreserveApiErrors(bool search)
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("{\"error\":{\"message\":\"Rate limit exceeded\",\"type\":\"rate_limit_error\"}}", Encoding.UTF8, "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        if (search)
        {
            Assert.Null(await client.SearchAsync(new SearchRequest { Query = "DeepSeek" }));
        }
        else
        {
            Assert.Null(await client.FetchAsync(new FetchRequest { Url = "https://example.com/" }));
        }

        Assert.Contains("TooManyRequests", client.ErrorMsg);
        Assert.Contains("Rate limit exceeded", client.ErrorMsg);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SearchAndFetch_ClearPreviousErrorsOnSuccess(bool firstSearch, bool secondSearch)
    {
        var calls = 0;
        var handler = new CapturingHandler(request => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent("rate limited"),
            }
            : JsonResponse(request.RequestUri!.AbsolutePath.EndsWith("search", StringComparison.Ordinal)
                ? "{\"results\":[]}"
                : "{\"title\":\"\",\"content\":\"Markdown\"}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        object? failed = firstSearch
            ? await client.SearchAsync(new SearchRequest { Query = "DeepSeek" })
            : await client.FetchAsync(new FetchRequest { Url = "https://example.com/" });
        Assert.Null(failed);
        Assert.Contains("TooManyRequests", client.ErrorMsg);

        object? succeeded = secondSearch
            ? await client.SearchAsync(new SearchRequest { Query = "DeepSeek" })
            : await client.FetchAsync(new FetchRequest { Url = "https://example.com/" });
        Assert.NotNull(succeeded);
        Assert.Null(client.ErrorMsg);
    }

    [Theory]
    [InlineData(true, "null")]
    [InlineData(true, "{}")]
    [InlineData(true, "{\"results\":null}")]
    [InlineData(true, "{\"results\":[null]}")]
    [InlineData(true, "{\"results\":{}}")]
    [InlineData(true, "[]")]
    [InlineData(true, "<html>Bad gateway</html>")]
    [InlineData(true, "{\"results\":")]
    [InlineData(false, "null")]
    [InlineData(false, "{}")]
    [InlineData(false, "{\"title\":\"Page\"}")]
    [InlineData(false, "{\"content\":\"Markdown\"}")]
    [InlineData(false, "{\"title\":null,\"content\":\"Markdown\"}")]
    [InlineData(false, "{\"title\":\"Page\",\"content\":null}")]
    [InlineData(false, "{\"title\":\"Page\",\"content\":42}")]
    [InlineData(false, "[]")]
    [InlineData(false, "<html>Bad gateway</html>")]
    public async Task SearchAndFetch_RejectInvalidSuccessResponses(bool search, string json)
    {
        var calls = 0;
        var handler = new CapturingHandler(_ => JsonResponse(++calls == 1 ? json :
            search ? "{\"results\":[]}" : "{\"title\":\"\",\"content\":\"Markdown\"}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        object? response = search
            ? await client.SearchAsync(new SearchRequest { Query = "DeepSeek" })
            : await client.FetchAsync(new FetchRequest { Url = "https://example.com/" });

        Assert.Null(response);
        Assert.Contains(search ? "invalid Search API response" : "invalid Fetch API response", client.ErrorMsg);

        // A later valid response must recover from a response-validation failure too.
        object? recovered = search
            ? await client.SearchAsync(new SearchRequest { Query = "DeepSeek" })
            : await client.FetchAsync(new FetchRequest { Url = "https://example.com/" });
        Assert.NotNull(recovered);
        Assert.Null(client.ErrorMsg);
    }

    private const string NullableSearchResult = """
        {"rerank_score":0,"title":"Page","url":"https://example.com/",
         "snippet":"","summary":"","content":"","site_name":null,
         "site_icon":null,"date":null,"authority":-1}
        """;

    public static IEnumerable<object[]> InvalidSearchResults()
    {
        // Every documented result field is required, including the nullable metadata fields.
        var valid = JsonNode.Parse(NullableSearchResult)!.AsObject();
        foreach (var field in valid.Select(property => property.Key).ToArray())
        {
            var missing = JsonNode.Parse(NullableSearchResult)!.AsObject();
            missing.Remove(field);
            yield return [missing.ToJsonString()];
        }

        foreach (var field in new[] { "title", "url", "snippet", "summary", "content" })
        {
            var withNull = JsonNode.Parse(NullableSearchResult)!.AsObject();
            withNull[field] = null;
            yield return [withNull.ToJsonString()];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidSearchResults))]
    public async Task Search_RejectsIncompleteResults(string resultJson)
    {
        var handler = new CapturingHandler(_ => JsonResponse("{\"results\":[" + resultJson + "]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        Assert.Null(await client.SearchAsync(new SearchRequest { Query = "DeepSeek" }));
        Assert.Contains("invalid Search API response", client.ErrorMsg);
    }

    [Fact]
    public async Task Search_AllowsNullableMetadataAndUnknownFields()
    {
        var result = JsonNode.Parse(NullableSearchResult)!.AsObject();
        result["future_field"] = "ignored";
        var handler = new CapturingHandler(_ => JsonResponse("{\"results\":[" + result.ToJsonString() + "]}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);

        var response = await client.SearchAsync(new SearchRequest { Query = "DeepSeek" });

        Assert.NotNull(response);
        var item = Assert.Single(response!.Results);
        Assert.Null(item.Date);
        Assert.Null(item.SiteName);
        Assert.Null(item.SiteIcon);
        Assert.Empty(item.Content);
        Assert.Equal(-1, item.Authority);
        Assert.Null(client.ErrorMsg);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SearchAndFetch_PropagateCancellation(bool search)
    {
        var handler = new CapturingHandler(_ => JsonResponse("{}"));
        using var httpClient = new HttpClient(handler);
        using var client = new DeepSeekClient(httpClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (search)
            {
                await client.SearchAsync(new SearchRequest { Query = "DeepSeek" }, cancellation.Token);
            }
            else
            {
                await client.FetchAsync(new FetchRequest { Url = "https://example.com/" }, cancellation.Token);
            }
        });
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
                    Authorization = request.Headers.Authorization?.ToString(),
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
        public string? Authorization { get; init; }
        public string? ContentType { get; init; }
    }
}
