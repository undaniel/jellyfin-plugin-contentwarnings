using Jellyfin.Plugin.ContentWarnings.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class WarningCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "content-warnings-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Matches_SurviveAReload()
    {
        var cache = CreateCache();
        var itemId = Guid.NewGuid();

        await cache.SaveMatchAsync(
            itemId,
            new MatchEntry { DtddId = 123, Method = MatchMethods.Imdb, CheckedAt = DateTimeOffset.UtcNow },
            CancellationToken.None);

        var reloaded = CreateCache();
        var entry = reloaded.GetMatch(itemId);

        Assert.NotNull(entry);
        Assert.Equal(123, entry!.DtddId);
        Assert.Equal(MatchMethods.Imdb, entry.Method);
    }

    [Fact]
    public async Task Media_SurvivesAReload()
    {
        var cache = CreateCache();
        var media = new CachedMedia
        {
            DtddId = 123,
            FetchedAt = DateTimeOffset.UtcNow,
            Topics = [new CachedTopicVote { Id = 153, Name = "a dog dies", YesSum = 10, NoSum = 1 }]
        };

        await cache.SaveMediaAsync(media, CancellationToken.None);

        var reloaded = CreateCache();
        var loaded = await reloaded.GetMediaAsync(123, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(10, loaded!.Topics.Single().YesSum);
    }

    [Fact]
    public async Task Topics_SurviveAReload()
    {
        var cache = CreateCache();
        await cache.SaveTopicsAsync(
            new TopicsCache
            {
                FetchedAt = DateTimeOffset.UtcNow,
                Topics = [new CachedTopicInfo { Id = 153, Name = "a dog dies" }]
            },
            CancellationToken.None);

        var loaded = await CreateCache().GetTopicsAsync(CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(153, loaded!.Topics.Single().Id);
    }

    [Fact]
    public void CorruptMatchFileDoesNotThrow()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "matches.json"), "{not json");

        var cache = CreateCache();

        Assert.Empty(cache.GetMatches());
    }

    [Fact]
    public async Task WritesAreAtomicAndLeaveNoTemporaryFile()
    {
        var cache = CreateCache();
        await cache.SaveMatchAsync(
            Guid.NewGuid(),
            new MatchEntry { Method = MatchMethods.NoMatch, CheckedAt = DateTimeOffset.UtcNow },
            CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(_directory, "matches.json.tmp")));
        Assert.True(File.Exists(Path.Combine(_directory, "matches.json")));
    }

    private WarningCache CreateCache() =>
        new(_directory, NullLogger<WarningCache>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
