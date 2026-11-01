using Jellyfin.Plugin.ContentWarnings.Imaging;
using SkiaSharp;

namespace Jellyfin.Plugin.ContentWarnings.Tests;

public class BadgeImageTests
{
    [Fact]
    public void Render_DrawsTheBadgeOnTheTopRightAndKeepsTheDimensions()
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

            var output = BadgeImage.Render(source, directory, Guid.NewGuid());

            using var result = SKBitmap.Decode(output);
            Assert.NotNull(result);
            Assert.Equal(200, result.Width);
            Assert.Equal(300, result.Height);
            Assert.NotEqual(SKColors.Gray, result.GetPixel(185, 15));
            Assert.Equal(SKColors.Gray, result.GetPixel(10, 290));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
