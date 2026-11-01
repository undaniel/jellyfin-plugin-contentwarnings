using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class DefaultTopicsTests
{
    [Theory]
    [InlineData("a dog dies", "Dog Death")]
    [InlineData("a cat dies", "Cat Death")]
    [InlineData("an animal dies", "Animal Death")]
    [InlineData("there are spiders", "Spiders")]
    [InlineData("there is self-harm", "Self-Harm")]
    [InlineData("there are flashing lights", "Flashing Lights")]
    [InlineData("a child dies", "Child Death")]
    public void Match_ReturnsCuratedLabel(string topicName, string expected)
    {
        Assert.Equal(expected, DefaultTopics.GetLabel(topicName));
    }

    [Fact]
    public void Match_UsesDoesNameWhenTopicNameIsGeneric()
    {
        Assert.True(DefaultTopics.IsDefault("trigger", "Does the dog die"));
    }

    [Fact]
    public void Match_PrefersSpecificRule()
    {
        var rule = DefaultTopics.Match("a dog dies");

        Assert.NotNull(rule);
        Assert.Equal("dog-death", rule!.Key);
    }

    [Fact]
    public void Match_UnknownTopicIsNotDefault()
    {
        Assert.False(DefaultTopics.IsDefault("there is a car crash"));
        Assert.Null(DefaultTopics.GetLabel("there is a car crash"));
    }

    [Fact]
    public void Rules_AreUnique()
    {
        Assert.Equal(DefaultTopics.Rules.Count, DefaultTopics.Rules.Select(rule => rule.Key).Distinct().Count());
    }
}
