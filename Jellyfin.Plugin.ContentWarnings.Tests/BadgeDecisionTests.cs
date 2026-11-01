using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class BadgeDecisionTests
{
    [Theory]
    [InlineData(true, "CW: ", new[] { "Action", "CW: Dog Death" }, true)]
    [InlineData(false, "CW: ", new[] { "CW: Dog Death" }, false)]
    [InlineData(true, "", new[] { "CW: Dog Death" }, false)]
    [InlineData(true, "CW: ", new[] { "Action", "Drama" }, false)]
    [InlineData(true, "CW: ", new string[0], false)]
    public void ShouldBadge_OnlyWhenEnabledAndATagMatchesThePrefix(bool enabled, string prefix, string[] tags, bool expected)
    {
        Assert.Equal(expected, BadgeDecision.ShouldBadge(enabled, prefix, tags));
    }

    [Fact]
    public void ShouldBadge_MatchesThePrefixCaseInsensitively()
    {
        Assert.True(BadgeDecision.ShouldBadge(true, "CW: ", ["cw: dog death"]));
    }

    [Fact]
    public void ShouldBadge_RejectsNullTags()
    {
        Assert.False(BadgeDecision.ShouldBadge(true, "CW: ", null));
    }

    [Fact]
    public void Fingerprint_IgnoresTagOrder()
    {
        var first = BadgeDecision.Fingerprint("CW: ", "TopRight", ["CW: A", "CW: B"]);
        var second = BadgeDecision.Fingerprint("CW: ", "TopRight", ["CW: B", "CW: A"]);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Fingerprint_ChangesWhenTheWarningsChange()
    {
        var first = BadgeDecision.Fingerprint("CW: ", "TopRight", ["CW: A"]);
        var second = BadgeDecision.Fingerprint("CW: ", "TopRight", ["CW: A", "CW: B"]);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Fingerprint_ChangesWhenTheCornerChanges()
    {
        var first = BadgeDecision.Fingerprint("CW: ", "TopRight", ["CW: A"]);
        var second = BadgeDecision.Fingerprint("CW: ", "BottomLeft", ["CW: A"]);

        Assert.NotEqual(first, second);
    }
}
