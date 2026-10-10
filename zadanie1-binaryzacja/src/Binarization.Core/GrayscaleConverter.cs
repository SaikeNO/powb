namespace Binarization.Core;

/// <summary>Grayscale pixels and the same source composited onto white for preview.</summary>
public sealed record GrayscaleConversion(GrayImage Gray, byte[] OpaqueBgra);

public static class GrayscaleConverter
{
    /// <summary>
    /// Converts tightly packed, straight-alpha BGRA32 pixels without modifying the input.
    /// Composites onto white before computing luminance with midpoint-to-even rounding.
    /// </summary>
    public static GrayscaleConversion ConvertBgra32(int width, int height, ReadOnlySpan<byte> bgra)
    {
        if (width <= 0 || height <= 0 || bgra.Length % 4 != 0 || (long)width * height != bgra.Length / 4)
            throw new ArgumentException("Image dimensions must match a nonempty packed BGRA32 buffer.");

        var opaque = new byte[bgra.Length];
        var gray = new byte[bgra.Length / 4];
        for (int i = 0; i < gray.Length; i++)
        {
            int j = i * 4;
            double a = bgra[j + 3] / 255.0;
            double b = bgra[j] * a + 255 * (1 - a);
            double g = bgra[j + 1] * a + 255 * (1 - a);
            double r = bgra[j + 2] * a + 255 * (1 - a);
            gray[i] = (byte)Math.Clamp(Math.Round(0.299 * r + 0.587 * g + 0.114 * b), 0, 255);
            opaque[j] = (byte)Math.Round(b);
            opaque[j + 1] = (byte)Math.Round(g);
            opaque[j + 2] = (byte)Math.Round(r);
            opaque[j + 3] = 255;
        }
        return new(new GrayImage(width, height, gray), opaque);
    }
}
