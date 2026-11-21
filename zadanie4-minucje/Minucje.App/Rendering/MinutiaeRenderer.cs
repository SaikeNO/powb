using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Minucje.Core.Analysis;
using Minucje.Core.Models;

namespace Minucje.Rendering;

public static class MinutiaeRenderer
{
    private const int Scale = 3;
    private static readonly Color ScannerBackgroundTint = Color.FromRgb(240, 228, 186);

    public static BitmapSource Render(FingerprintAnalysis analysis, byte[] grayscale, ViewMode mode,
        ISet<MinutiaType> visibleTypes, ISet<RejectionReason> visibleReasons)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);

        using (DrawingContext context = visual.RenderOpen())
        {
            var imageRect = new Rect(0, 0, analysis.Width * Scale, analysis.Height * Scale);
            context.DrawImage(DrawBackground(analysis, grayscale, mode), imageRect);

            foreach (RejectedMinutia rejected in analysis.Rejected)
            {
                if (visibleReasons.Contains(rejected.Reason))
                {
                    Legend.DrawRejectedMarker(context, CenterOf(rejected.Minutia), 5 * Scale);
                }
            }

            foreach (Minutia minutia in analysis.Minutiae)
            {
                if (!visibleTypes.Contains(minutia.Type))
                {
                    continue;
                }

                var pixel = new Rect(minutia.X * Scale, minutia.Y * Scale, Scale, Scale);
                context.DrawRectangle(new SolidColorBrush(Legend.ColorOf(minutia.Type)), null, pixel);
                Legend.DrawMarker(context, minutia.Type, CenterOf(minutia), 5 * Scale);
            }
        }

        var bitmap = new RenderTargetBitmap(analysis.Width * Scale, analysis.Height * Scale, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static Point CenterOf(Minutia minutia)
    {
        return new Point((minutia.X + 0.5) * Scale, (minutia.Y + 0.5) * Scale);
    }

    private static BitmapSource DrawBackground(FingerprintAnalysis analysis, byte[] grayscale, ViewMode mode)
    {
        int width = analysis.Width;
        int height = analysis.Height;
        var bgraPixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = y * width + x;
                int paddedIndex = (y + 1) * (width + 2) + (x + 1);
                Color color = PixelColor(analysis, grayscale[pixelIndex], paddedIndex, mode);

                if (!analysis.Mask[pixelIndex])
                {
                    color = Mix(color, ScannerBackgroundTint);
                }

                int byteIndex = pixelIndex * 4;
                bgraPixels[byteIndex] = color.B;
                bgraPixels[byteIndex + 1] = color.G;
                bgraPixels[byteIndex + 2] = color.R;
                bgraPixels[byteIndex + 3] = 255;
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bgraPixels, width * 4);
    }

    private static Color PixelColor(FingerprintAnalysis analysis, byte brightness, int paddedIndex, ViewMode mode)
    {
        bool isRidge = analysis.Binary[paddedIndex] == 1;
        bool isSkeleton = analysis.Skeleton[paddedIndex] == 1;

        switch (mode)
        {
            case ViewMode.Binary:
                return isRidge ? Colors.Black : Colors.White;
            case ViewMode.Skeleton:
                return isSkeleton ? Colors.Black : Colors.White;
            default:
                return Color.FromRgb(brightness, brightness, brightness);
        }
    }

    private static Color Mix(Color pixelColor, Color tint)
    {
        return Color.FromRgb(
            (byte)((pixelColor.R + tint.R) / 2),
            (byte)((pixelColor.G + tint.G) / 2),
            (byte)((pixelColor.B + tint.B) / 2));
    }
}
