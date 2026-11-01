using Jellyfin.Plugin.ContentWarnings.Imaging;
using SkiaSharp;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class BadgeImageTests
{
    [Theory]
    [InlineData(BadgeCorner.TopRight)]
    [InlineData(BadgeCorner.TopLeft)]
    [InlineData(BadgeCorner.BottomRight)]
    [InlineData(BadgeCorner.BottomLeft)]
    public void Render_DrawsTheBadgeInTheRequestedCornerAndKeepsTheDimensions(BadgeCorner corner)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cw-badge-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);

        try
        {
            var source = Path.Combine(directory, "source.png");
            using (var bitmap = new SKBitmap(200, 300))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.Gray);
                }

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(source);
                data.SaveTo(stream);
            }

            var output = BadgeImage.Render(source, directory, Guid.NewGuid(), corner);

            using var result = SKBitmap.Decode(output);
            Assert.NotNull(result);
            Assert.Equal(200, result.Width);
            Assert.Equal(300, result.Height);

            var badgeX = corner is BadgeCorner.TopLeft or BadgeCorner.BottomLeft ? 23 : 177;
            var badgeY = corner is BadgeCorner.TopLeft or BadgeCorner.TopRight ? 23 : 277;
            var oppositeX = badgeX == 23 ? 177 : 23;
            var oppositeY = badgeY == 23 ? 277 : 23;

            Assert.NotEqual(SKColors.Gray, result.GetPixel(badgeX, badgeY));
            Assert.Equal(SKColors.Gray, result.GetPixel(oppositeX, oppositeY));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(null, BadgeCorner.TopRight)]
    [InlineData("BottomLeft", BadgeCorner.BottomLeft)]
    [InlineData("bogus", BadgeCorner.TopRight)]
    public void ParseCorner_FallsBackToTheTopRight(string? value, BadgeCorner expected)
    {
        Assert.Equal(expected, BadgeImage.ParseCorner(value));
    }
}
