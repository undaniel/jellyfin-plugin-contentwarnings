using Jellyfin.Plugin.ContentWarnings.Core;
using SkiaSharp;

namespace Jellyfin.Plugin.ContentWarnings.Imaging;

/// <summary>
/// Corner of the image that receives the badge.
/// </summary>
public enum BadgeCorner
{
    /// <summary>Top right corner.</summary>
    TopRight,

    /// <summary>Top left corner.</summary>
    TopLeft,

    /// <summary>Bottom right corner.</summary>
    BottomRight,

    /// <summary>Bottom left corner.</summary>
    BottomLeft
}

/// <summary>
/// Draws the warning badge onto a copy of an image. Uses the SkiaSharp assembly that
/// Jellyfin already ships, so no native libraries are bundled with the plugin.
/// </summary>
public static class BadgeImage
{
    private static readonly SKColor BadgeColor = new(0xD3, 0x2F, 0x2F);
    private static readonly SKColor MarkColor = SKColors.White;

    /// <summary>
    /// Parses a configured corner name, falling back to the top right.
    /// </summary>
    /// <param name="value">Configured value.</param>
    /// <returns>The corner.</returns>
    public static BadgeCorner ParseCorner(string? value)
        => Enum.TryParse<BadgeCorner>(value, true, out var corner) && Enum.IsDefined(corner)
            ? corner
            : BadgeCorner.TopRight;

    /// <summary>
    /// Composites the badge and writes the result to the target directory.
    /// </summary>
    /// <param name="sourcePath">Image to draw on.</param>
    /// <param name="targetDirectory">Directory that receives the badged image.</param>
    /// <param name="itemId">Item id, used as the file name.</param>
    /// <param name="corner">Corner that receives the badge.</param>
    /// <param name="suffix">Extra file name segment, used to keep image types apart.</param>
    /// <returns>The path of the badged image.</returns>
    public static string Render(
        string sourcePath,
        string targetDirectory,
        Guid itemId,
        BadgeCorner corner,
        string suffix = "")
    {
        using var original = SKBitmap.Decode(sourcePath)
            ?? throw new InvalidOperationException($"Could not decode {sourcePath} as an image.");

        var keepAlpha = Path.GetExtension(sourcePath).ToLowerInvariant() is ".png" or ".webp" or ".gif";
        var outputPath = Path.Combine(
            targetDirectory,
            $"{itemId}-{BadgeDecision.Version}{suffix}{(keepAlpha ? ".png" : ".jpg")}");

        using var bitmap = new SKBitmap(original.Width, original.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.DrawBitmap(original, 0, 0);
            DrawBadge(canvas, original.Width, original.Height, corner);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(
            keepAlpha ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
            keepAlpha ? 100 : 94);

        var temporary = outputPath + ".tmp";
        using (var stream = File.Create(temporary))
        {
            data.SaveTo(stream);
        }

        File.Move(temporary, outputPath, true);
        return outputPath;
    }

    private static void DrawBadge(SKCanvas canvas, int width, int height, BadgeCorner corner)
    {
        var radius = Math.Max(12f, Math.Min(width, height) * 0.08f);
        var margin = radius * 0.45f;
        var centerX = corner is BadgeCorner.TopRight or BadgeCorner.BottomRight
            ? width - margin - radius
            : margin + radius;
        var centerY = corner is BadgeCorner.BottomRight or BadgeCorner.BottomLeft
            ? height - margin - radius
            : margin + radius;

        using var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 82), IsAntialias = true };
        canvas.DrawCircle(centerX, centerY, radius * 1.065f, shadow);

        using var fill = new SKPaint { Color = BadgeColor, IsAntialias = true };
        canvas.DrawCircle(centerX, centerY, radius, fill);

        // A hairline rim keeps the badge readable on posters that are red or dark red.
        using var rim = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 87),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = radius * 0.035f
        };
        canvas.DrawCircle(centerX, centerY, radius * 0.9825f, rim);

        // Warning triangle with the paw knocked out, same proportions as the plugin icon.
        var halfBase = radius * 0.783f;
        using var triangle = new SKPath();
        triangle.MoveTo(centerX, centerY - (radius * 0.633f));
        triangle.LineTo(centerX + halfBase, centerY + (radius * 0.700f));
        triangle.LineTo(centerX - halfBase, centerY + (radius * 0.700f));
        triangle.Close();

        using var mark = new SKPaint
        {
            Color = MarkColor,
            IsAntialias = true,
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = radius * 0.108f,
            StrokeJoin = SKStrokeJoin.Round
        };
        canvas.DrawPath(triangle, mark);

        using var paw = new SKPaint { Color = BadgeColor, IsAntialias = true };
        canvas.DrawOval(centerX, centerY + (radius * 0.408f), radius * 0.225f, radius * 0.1875f, paw);
        DrawToe(canvas, centerX - (radius * 0.267f), centerY + (radius * 0.167f), -22f, radius, paw);
        DrawToe(canvas, centerX - (radius * 0.0833f), centerY + (radius * 0.067f), -8f, radius, paw);
        DrawToe(canvas, centerX + (radius * 0.0833f), centerY + (radius * 0.067f), 8f, radius, paw);
        DrawToe(canvas, centerX + (radius * 0.267f), centerY + (radius * 0.167f), 22f, radius, paw);
    }

    private static void DrawToe(
        SKCanvas canvas,
        float x,
        float y,
        float angle,
        float radius,
        SKPaint paint)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(angle);
        canvas.DrawOval(0, 0, radius * 0.0958f, radius * 0.1208f, paint);
        canvas.Restore();
    }
}
