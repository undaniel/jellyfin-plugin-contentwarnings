using System.Xml.Serialization;
using Jellyfin.Plugin.ContentWarnings.Configuration;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class PluginConfigurationTests
{
    [Fact]
    public void XmlRoundTrip_KeepsEverySetting()
    {
        var configuration = new PluginConfiguration
        {
            ApiKey = "ddd_test",
            TagPrefix = "AVISO: ",
            MinYesVotes = 5,
            MinYesRatio = 0.75,
            EnabledTopicIds = [153, 167],
            TopicLabelOverrides = [new TopicLabelOverride { TopicId = 153, Label = "Muerte de perro" }],
            IncludeSpoilerTopics = true,
            LibraryIds = [Guid.NewGuid()],
            CacheTtlDays = 10,
            NotFoundRetryDays = 3,
            RequestsPerSecond = 0.4,
            ProcessOnItemAdded = false
        };

        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var stream = new MemoryStream();
        serializer.Serialize(stream, configuration);
        stream.Position = 0;
        var reloaded = (PluginConfiguration)serializer.Deserialize(stream)!;

        Assert.Equal(configuration.ApiKey, reloaded.ApiKey);
        Assert.Equal(configuration.TagPrefix, reloaded.TagPrefix);
        Assert.Equal(5, reloaded.MinYesVotes);
        Assert.Equal(0.75, reloaded.MinYesRatio);
        Assert.Equal([153, 167], reloaded.EnabledTopicIds);
        Assert.Equal("Muerte de perro", reloaded.TopicLabelOverrides.Single().Label);
        Assert.True(reloaded.IncludeSpoilerTopics);
        Assert.Equal(configuration.LibraryIds.Single(), reloaded.LibraryIds.Single());
        Assert.Equal(0.4, reloaded.RequestsPerSecond);
        Assert.False(reloaded.ProcessOnItemAdded);
    }
}
