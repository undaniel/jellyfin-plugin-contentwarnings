using Jellyfin.Plugin.ContentWarnings.Core;
using SkiaSharp;

namespace Jellyfin.Plugin.ContentWarnings.Imaging;

/// <summary>
/// Draws the warning badge onto a copy of an image. Uses the SkiaSharp assembly that
/// Jellyfin already ships, so no native libraries are bundled with the plugin.
/// </summary>
public static class BadgeImage
{
    private static readonly SKColor BadgeColor = new(0xD3, 0x2F, 0x2F);
    private static readonly SKColor MarkColor = SKColors.White;

    /// <summary>
    /// Composites the badge and writes the result to the target directory.
    /// </summary>
    /// <param name="sourcePath">Image to draw on.</param>
    /// <param name="targetDirectory">Directory that receives the badged image.</param>
    /// <param name="itemId">Item id, used as the file name.</param>
    /// <returns>The path of the badged image.</returns>
    public static string Render(string sourcePath, string targetDirectory, Guid itemId)
    {
        using var original = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidOperationException($"Could not decode {sourcePath} as an image.");

        var keepAlpha = Path.GetExtension(sourcePath).ToLowerInvariant() is ".png" or ".webp" or ".gif";
        var outputPath = Path.Combine(targetDirectory, $"{itemId}-{BadgeDecision.Version}{(keepAlpha ? ".png" : ".jpg")}");

        using var bitmap = new SKBitmap(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.DrawBitmap(original, 0, 0);
            DrawBadge(canvas, original.Width, original.Height);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            keepAlpha ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
            keepAlpha ? 100 : 90);

        var temporary = outputPath + ".tmp";
        using (var stream = File.Create(temporary))
        {
            data.SaveTo(stream);
        }

        File.Move(temporary, outputPath, true);
        return outputPath;
    }

    private static void DrawBadge(SKCanvas canvas, int width, int height)
    {
        var radius = Math.Max(12f, Math.Min(width, height) * 0.08f);
        var margin = radius * 0.45f;
        var centerX = width - margin - radius;
        var centerY = margin + radius;

        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 90), IsAntialias = true };
        canvas.DrawCircle(centerX, centerY, radius * 1.06f, shadow);

        using var fill = new SKPaint { Color = BadgeColor, IsAntialias = true };
        canvas.DrawCircle(centerX, centerY, radius, fill);

        using var mark = new SKPaint { Color = MarkColor, IsAntialias = true };
        var barWidth = radius * 0.26f;
        var barTop = centerY - radius * 0.62f;
        var barBottom = barTop + radius * 0.95f;
        canvas.DrawRoundRect(
            new SKRect(centerX - (barWidth / 2), barTop, centerX + (barWidth / 2), barBottom),
            barWidth / 2,
            barWidth / 2,
            mark);
        canvas.DrawCircle(centerX, barBottom + (radius * 0.20f), barWidth * 0.72f, mark);
    }
}
