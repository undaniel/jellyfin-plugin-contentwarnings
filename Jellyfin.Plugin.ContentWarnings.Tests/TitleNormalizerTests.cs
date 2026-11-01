using Jellyfin.Plugin.ContentWarnings.Core;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class TitleNormalizerTests
{
    [Theory]
    [InlineData("The Thing", "thing")]
    [InlineData("A Quiet Place", "quiet place")]
    [InlineData("El laberinto del fauno", "laberinto del fauno")]
    [InlineData("  Spider-Man: No Way Home! ", "spider man no way home")]
    [InlineData("Amélie", "amelie")]
    [InlineData("Los Otros", "otros")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_RemovesNoise(string? input, string expected)
    {
        Assert.Equal(expected, TitleNormalizer.Normalize(input));
    }

    [Fact]
    public void Similarity_IgnoresArticlesAndPunctuation()
    {
        Assert.Equal(1.0, TitleNormalizer.Similarity("The Thing", "thing"), 3);
        Assert.Equal(1.0, TitleNormalizer.Similarity("Spider-Man", "spider man"), 3);
    }

    [Fact]
    public void Similarity_IsProportional()
    {
        var similarity = TitleNormalizer.Similarity("Blade Runner", "Blade Runners");

        Assert.True(similarity >= 0.9);
        Assert.True(similarity < 1.0);
    }

    [Fact]
    public void Similarity_EmptyStrings()
    {
        Assert.Equal(1.0, TitleNormalizer.Similarity(null, ""), 3);
        Assert.Equal(0.0, TitleNormalizer.Similarity("Alien", ""), 3);
    }
}
