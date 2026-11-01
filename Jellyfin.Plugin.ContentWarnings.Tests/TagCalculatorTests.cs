using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class TagCalculatorTests
{
    [Fact]
    public void Merge_NeverRemovesTagsWithoutThePrefix()
    {
        var result = TagCalculator.Merge(
            ["Action", "My Favourite", "CW: Old Tag"],
            ["CW: Dog Death"],
            "CW: ");

        Assert.Contains("Action", result);
        Assert.Contains("My Favourite", result);
        Assert.DoesNotContain("CW: Old Tag", result);
        Assert.Contains("CW: Dog Death", result);
    }

    [Fact]
    public void Merge_DeduplicatesCaseInsensitively()
    {
        var result = TagCalculator.Merge(["cw: dog death"], ["CW: Dog Death"], "CW: ");

        Assert.Single(result);
        Assert.Equal("CW: Dog Death", result[0]);
    }

    [Fact]
    public void Merge_PreservesOrderOfUnrelatedTags()
    {
        var result = TagCalculator.Merge(["Zeta", "Alpha", "CW: X"], ["CW: A"], "CW: ");

        Assert.Equal(["Zeta", "Alpha", "CW: A"], result);
    }

    [Fact]
    public void Merge_EmptyDesiredRemovesOnlyPluginTags()
    {
        var result = TagCalculator.Merge(["Action", "CW: Dog Death"], [], "CW: ");

        Assert.Equal(["Action"], result);
    }

    [Fact]
    public void Merge_EmptyPrefixIsANoOp()
    {
        var result = TagCalculator.Merge(["Action", "CW: Dog Death"], ["New"], "  ");

        Assert.Equal(["Action", "CW: Dog Death"], result);
    }

    [Fact]
    public void Merge_IgnoresBlankTags()
    {
        var result = TagCalculator.Merge(["Action", ""], ["CW: A", ""], "CW: ");

        Assert.Equal(["Action", "CW: A"], result);
    }

    [Fact]
    public void AreEqual_IsAnOrderInsensitiveCaseInsensitiveSetComparison()
    {
        Assert.True(TagCalculator.AreEqual(["CW: A", "Action"], ["action", "cw: a"]));
        Assert.False(TagCalculator.AreEqual(["CW: A"], ["CW: A", "CW: B"]));
    }

    [Fact]
    public void AreEqual_HandlesNull()
    {
        Assert.True(TagCalculator.AreEqual(null, []));
        Assert.False(TagCalculator.AreEqual(null, ["CW: A"]));
    }
}
