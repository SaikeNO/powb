namespace Minucje.Core.Preprocessing;

// średnia jasność w kwadracie wokół każdego piksela; liczona osobno w poziomie i w pionie
public static class BoxBlur
{
    public static byte[] Apply(byte[] image, int width, int height, int radius)
    {
        var blurredRows = new byte[image.Length];
        for (int y = 0; y < height; y++)
        {
            BlurLine(image, blurredRows, start: y * width, step: 1, length: width, radius);
        }

        var blurred = new byte[image.Length];
        for (int x = 0; x < width; x++)
        {
            BlurLine(blurredRows, blurred, start: x, step: width, length: height, radius);
        }
        return blurred;
    }

    // okno przesuwane: dodajemy piksel, który wchodzi do okna, odejmujemy ten, który wychodzi
    private static void BlurLine(byte[] source, byte[] target, int start, int step, int length, int radius)
    {
        int windowSum = 0;
        for (int i = 0; i <= Math.Min(radius, length - 1); i++)
        {
            windowSum += source[start + i * step];
        }

        for (int i = 0; i < length; i++)
        {
            int windowStart = Math.Max(0, i - radius);
            int windowEnd = Math.Min(length - 1, i + radius);
            int windowSize = windowEnd - windowStart + 1;
            target[start + i * step] = (byte)(windowSum / windowSize);

            int entering = i + radius + 1;
            int leaving = i - radius;
            if (entering < length)
            {
                windowSum += source[start + entering * step];
            }
            if (leaving >= 0)
            {
                windowSum -= source[start + leaving * step];
            }
        }
    }
}
