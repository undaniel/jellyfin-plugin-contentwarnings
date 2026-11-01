using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Jellyfin.Plugin.ContentWarnings.Configuration;
using Jellyfin.Plugin.ContentWarnings.Dtdd;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class DtddClientTests
{
    private static string TestData(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", name));

    private static (DtddClient Client, StubHandler Handler, PluginConfiguration Config) CreateClient(
        Func<int, HttpResponseMessage> responder,
        int maxRetries = 5,
        string apiKey = "ddd_test")
    {
        var handler = new StubHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = DtddClient.BaseAddress };
        var config = new PluginConfiguration { ApiKey = apiKey, RequestsPerSecond = 100_000 };
        var client = new DtddClient(
            httpClient,
            () => config,
            NullLogger<DtddClient>.Instance,
            TimeSpan.FromMilliseconds(1),
            maxRetries);

        return (client, handler, config);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SearchByImdb_SendsKeyAndParsesResults()
    {
        var (client, handler, _) = CreateClient(_ => Json(TestData("dtdd-search-sample.json")));

        var results = await client.SearchByImdbAsync("tt0050798", CancellationToken.None);

        var item = Assert.Single(results);
        Assert.Equal(10752, item.Id);
        Assert.Equal("Old Yeller", item.Name);
        Assert.Equal(1957, item.ReleaseYear);
        Assert.Equal("tt0050798", item.ImdbId);
        Assert.Equal("ddd_test", handler.Requests[0].Headers.GetValues("X-API-KEY").Single());
        Assert.Contains("items?imdb=tt0050798", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task SearchByTitle_EncodesTitleAndYear()
    {
        var (client, handler, _) = CreateClient(_ => Json("[]"));

        await client.SearchByTitleAsync("Spider-Man: No Way Home", 2021, CancellationToken.None);

        var uri = handler.Requests[0].RequestUri!.AbsoluteUri;
        Assert.Contains("items?q=", uri);
        Assert.Contains("Spider-Man", uri);
        Assert.Contains("2021", uri);
        Assert.Contains("%20", uri);
    }

    [Fact]
    public async Task GetItem_ParsesTopicStats()
    {
        var (client, _, _) = CreateClient(_ => Json(TestData("dtdd-item-sample.json")));

        var item = await client.GetItemAsync(10752, CancellationToken.None);

        Assert.Equal(2, item.TopicItemStats.Count);
        Assert.Equal(153, item.TopicItemStats[0].TopicId);
        Assert.Equal(57, item.TopicItemStats[0].YesSum);
        Assert.Equal(3, item.TopicItemStats[0].NoSum);
    }

    [Fact]
    public async Task GetTopics_ParsesTaxonomy()
    {
        var (client, _, _) = CreateClient(_ => Json(TestData("dtdd-topics-sample.json")));

        var topics = await client.GetTopicsAsync(CancellationToken.None);

        Assert.Equal(4, topics.Count);
        Assert.Contains(topics, topic => topic.Id == 153 && topic.DoesName == "Does the dog die");
    }

    [Fact]
    public async Task RetriesRateLimitThenSucceeds()
    {
        var handler = new StubHandler(call =>
        {
            if (call == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
                return response;
            }

            return Json(TestData("dtdd-search-sample.json"));
        });

        var httpClient = new HttpClient(handler) { BaseAddress = DtddClient.BaseAddress };
        var client = new DtddClient(
            httpClient,
            () => new PluginConfiguration { ApiKey = "ddd_test", RequestsPerSecond = 100_000 },
            NullLogger<DtddClient>.Instance,
            TimeSpan.FromMilliseconds(1),
            1);

        var results = await client.SearchByImdbAsync("tt0050798", CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RejectsInvalidKeyWithoutRetrying()
    {
        var (client, handler, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<DtddAuthException>(() => client.SearchByImdbAsync("tt1", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task StopsAfterRetriesOnServerErrors()
    {
        var (client, handler, _) = CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            maxRetries: 2);

        await Assert.ThrowsAsync<DtddUnavailableException>(
            () => client.SearchByImdbAsync("tt1", CancellationToken.None));
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task NetworkErrorsBecomeUnavailableAfterRetries()
    {
        var (client, handler, _) = CreateClient(
            _ => throw new HttpRequestException("boom"),
            maxRetries: 1);

        await Assert.ThrowsAsync<DtddUnavailableException>(
            () => client.SearchByImdbAsync("tt1", CancellationToken.None));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task MissingKeyFailsWithoutRequests()
    {
        var (client, handler, _) = CreateClient(_ => Json("[]"), apiKey: "  ");

        await Assert.ThrowsAsync<DtddAuthException>(() => client.SearchByImdbAsync("tt1", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task CircuitBreakerOpensAfterTwentyConsecutiveErrors()
    {
        var (client, handler, _) = CreateClient(
            _ => new HttpResponseMessage(HttpStatusCode.BadGateway),
            maxRetries: 0);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Assert.ThrowsAsync<DtddUnavailableException>(
                () => client.SearchByImdbAsync("tt1", CancellationToken.None));
        }

        Assert.Equal(20, handler.Calls);
        await Assert.ThrowsAsync<DtddUnavailableException>(
            () => client.SearchByImdbAsync("tt1", CancellationToken.None));
        Assert.Equal(20, handler.Calls);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<int, HttpResponseMessage> _responder;

        public StubHandler(Func<int, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public int Calls { get; private set; }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            Requests.Add(request);
            var response = _responder(Calls);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
