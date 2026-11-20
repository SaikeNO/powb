namespace Minucje.Core.Preprocessing;

// Średnia jasność w kwadracie (2·radius+1)×(2·radius+1) wokół każdego piksela.
// Liczona osobno w poziomie (każdy wiersz) i w pionie (każda kolumna) - wynik jest ten sam, a liczenie szybsze.
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

    // Rozmycie jednej linii (wiersza albo kolumny) metodą przesuwanego okna:
    // zamiast za każdym razem sumować całe okno, dodajemy piksel, który do okna wchodzi,
    // i odejmujemy piksel, który z niego wychodzi. Przy krawędzi okno jest krótsze.
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

            int entering = i + radius + 1;  // piksel, który wejdzie do okna w następnym kroku
            int leaving = i - radius;       // piksel, który wyjdzie z okna w następnym kroku
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
