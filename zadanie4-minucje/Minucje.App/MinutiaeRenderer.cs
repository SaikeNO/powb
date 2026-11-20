using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Minucje.Core;

namespace Minucje;

public enum ViewMode
{
    Original,  // zdjęcie odcisku
    Skeleton,  // szkielet: linie grubości 1 piksela
    Binary,    // obraz po binaryzacji
}

// Rysuje odcisk z zaznaczonymi minucjami.
// Obraz jest powiększany 3 razy, żeby znaczniki były czytelne na małych odciskach (ok. 250×340 px).
public static class MinutiaeRenderer
{
    private const int Scale = 3;
    private static readonly Color OutsideFingerprint = Color.FromRgb(240, 228, 186);  // tło skanera - żółtawe

    // visibleTypes / visibleReasons: co jest zaznaczone checkboxami w tabelach, tylko to jest rysowane
    public static BitmapSource Render(FingerprintAnalysis analysis, byte[] grayscale, ViewMode mode,
        ISet<MinutiaType> visibleTypes, ISet<RejectionReason> visibleReasons)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);

        using (DrawingContext context = visual.RenderOpen())
        {
            // 1. tło: oryginał, szkielet albo obraz binarny
            var imageRect = new Rect(0, 0, analysis.Width * Scale, analysis.Height * Scale);
            context.DrawImage(DrawBackground(analysis, grayscale, mode), imageRect);

            // 2. odrzucone (fałszywe) minucje jako szare ×
            foreach (RejectedMinutia rejected in analysis.Rejected)
            {
                if (visibleReasons.Contains(rejected.Reason))
                {
                    Legend.DrawRejectedMarker(context, CenterOf(rejected.Minutia), 5 * Scale);
                }
            }

            // 3. minucje: piksel minucji wypełniony kolorem + kolorowy okrąg wokół
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

    // obraz w oryginalnym rozmiarze, piksel po pikselu (format BGRA: 4 bajty na piksel)
    private static BitmapSource DrawBackground(FingerprintAnalysis analysis, byte[] grayscale, ViewMode mode)
    {
        int width = analysis.Width;
        int height = analysis.Height;
        var pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                int paddedIndex = (y + 1) * (width + 2) + (x + 1);
                Color color = PixelColor(analysis, grayscale[i], paddedIndex, mode);

                if (!analysis.Mask[i])
                {
                    color = Mix(color, OutsideFingerprint);
                }

                pixels[i * 4] = color.B;
                pixels[i * 4 + 1] = color.G;
                pixels[i * 4 + 2] = color.R;
                pixels[i * 4 + 3] = 255;
            }
        }

        return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
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

    // pół na pół: kolor piksela i kolor tła skanera
    private static Color Mix(Color a, Color b)
    {
        return Color.FromRgb((byte)((a.R + b.R) / 2), (byte)((a.G + b.G) / 2), (byte)((a.B + b.B) / 2));
    }
}
