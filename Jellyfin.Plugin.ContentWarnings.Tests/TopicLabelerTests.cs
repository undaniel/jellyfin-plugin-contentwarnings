using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class TopicLabelerTests
{
    [Fact]
    public void ToTag_UsesCuratedDefaultLabel()
    {
        Assert.Equal("CW: Dog Death", TopicLabeler.ToTag(153, "a dog dies", "Does the dog die", null, "CW: "));
    }

    [Fact]
    public void ToTag_PrefersOverride()
    {
        var overrides = new Dictionary<int, string> { [153] = "Muerte de perro" };

        Assert.Equal("CW: Muerte de perro", TopicLabeler.ToTag(153, "a dog dies", null, overrides, "CW: "));
    }

    [Fact]
    public void ToLabel_StripsQuestionPrefix()
    {
        Assert.Equal("Loud noises", TopicLabeler.ToLabel(999, "trigger", "Are there loud noises?", null));
    }

    [Fact]
    public void ToLabel_StripsThereIsAndArticle()
    {
        Assert.Equal("Car crash", TopicLabeler.ToLabel(999, "there is a car crash", null, null));
    }

    [Fact]
    public void ToLabel_FallsBackToTopicId()
    {
        Assert.Equal("Topic 42", TopicLabeler.ToLabel(42, " ", null, null));
    }

    [Fact]
    public void ToLabel_IgnoresBlankOverride()
    {
        var overrides = new Dictionary<int, string> { [153] = "   " };

        Assert.Equal("Dog Death", TopicLabeler.ToLabel(153, "a dog dies", null, overrides));
    }
}
