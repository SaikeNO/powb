using System.Diagnostics;

namespace Binarization.Core;

public enum Algorithm { Threshold, Niblack, Sauvola, Phansalkar, Otsu }

public sealed record Parameters(int Threshold = 128, int WindowSize = 25,
    double NiblackK = -0.2, double SauvolaK = 0.5, double SauvolaR = 128,
    double PhansalkarK = 0.25, double PhansalkarR = 0.5, double P = 2, double Q = 10)
{
    public void Validate(Algorithm algorithm)
    {
        if (!Enum.IsDefined(algorithm)) throw new ArgumentOutOfRangeException(nameof(algorithm));
        if (algorithm == Algorithm.Threshold && Threshold is < 0 or > 255)
            throw new ArgumentException("Threshold must be between 0 and 255.");
        if (algorithm is Algorithm.Threshold or Algorithm.Otsu) return;
        if (WindowSize < 1 || WindowSize % 2 == 0)
            throw new ArgumentException("Window size must be a positive odd integer.");
        double[] values = algorithm switch
        {
            Algorithm.Niblack => [NiblackK],
            Algorithm.Sauvola => [SauvolaK, SauvolaR],
            _ => [PhansalkarK, PhansalkarR, P, Q]
        };
        if (values.Any(v => !double.IsFinite(v))) throw new ArgumentException("Parameters must be finite numbers.");
        if ((algorithm == Algorithm.Sauvola && SauvolaR <= 0) ||
            (algorithm == Algorithm.Phansalkar && PhansalkarR <= 0))
            throw new ArgumentException("R must be positive.");
    }
}

public sealed class GrayImage
{
    private readonly byte[] pixels;
    public int Width { get; }
    public int Height { get; }
    public ReadOnlyMemory<byte> Pixels => pixels;
    public GrayImage(int width, int height, ReadOnlySpan<byte> pixels)
    {
        if (width <= 0 || height <= 0 || (long)width * height != pixels.Length)
            throw new ArgumentException("Image dimensions must match a nonempty pixel buffer.");
        Width = width; Height = height; this.pixels = pixels.ToArray();
    }
}

public sealed record BinarizationResult(GrayImage Image, TimeSpan Elapsed, int? GlobalThreshold);

public static class Binarizer
{
    public static BinarizationResult Run(GrayImage image, Algorithm algorithm, Parameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var p = parameters ?? new Parameters();
        p.Validate(algorithm);
        var clock = Stopwatch.StartNew();
        var input = image.Pixels.Span;
        var output = new byte[input.Length];
        int? threshold = null;
        if (algorithm is Algorithm.Threshold or Algorithm.Otsu)
        {
            threshold = algorithm == Algorithm.Otsu ? Otsu(input) : p.Threshold;
            for (int i = 0; i < input.Length; i++) output[i] = input[i] > threshold.Value ? (byte)255 : (byte)0;
        }
        else
        {
            int w = image.Width, h = image.Height, stride = checked(w + 1);
            int size = checked(stride * checked(h + 1));
            var sums = new double[size];
            var squares = new double[size];
            for (int y = 0; y < h; y++)
            {
                double row = 0, rowSquares = 0;
                for (int x = 0; x < w; x++)
                {
                    double v = input[y * w + x];
                    row += v; rowSquares += v * v;
                    int i = (y + 1) * stride + x + 1;
                    sums[i] = sums[i - stride] + row;
                    squares[i] = squares[i - stride] + rowSquares;
                }
            }
            int radius = p.WindowSize / 2;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int left = Math.Max(0, x - radius), top = Math.Max(0, y - radius);
                int right = (int)Math.Min(w, (long)x + radius + 1), bottom = (int)Math.Min(h, (long)y + radius + 1);
                double count = (double)(right - left) * (bottom - top);
                double m = Rectangle(sums, stride, left, top, right, bottom) / count;
                double s = Math.Sqrt(Math.Max(0, Rectangle(squares, stride, left, top, right, bottom) / count - m * m));
                double t;
                if (algorithm == Algorithm.Niblack) t = m + p.NiblackK * s;
                else if (algorithm == Algorithm.Sauvola) t = m * (1 + p.SauvolaK * (s / p.SauvolaR - 1));
                else
                {
                    m /= 255; s /= 255;
                    t = 255 * m * (1 + p.P * Math.Exp(-p.Q * m) + p.PhansalkarK * (s / p.PhansalkarR - 1));
                }
                if (!double.IsFinite(t)) throw new ArgumentException("Parameters produce a nonfinite threshold. Use smaller values.");
                output[y * w + x] = input[y * w + x] > t ? (byte)255 : (byte)0;
            }
        }
        var resultImage = new GrayImage(image.Width, image.Height, output);
        clock.Stop();
        return new(resultImage, clock.Elapsed, threshold);
    }

    private static double Rectangle(double[] a, int stride, int l, int t, int r, int b) =>
        a[b * stride + r] - a[t * stride + r] - a[b * stride + l] + a[t * stride + l];

    private static int Otsu(ReadOnlySpan<byte> pixels)
    {
        var histogram = new long[256];
        double totalSum = 0;
        foreach (byte value in pixels) { histogram[value]++; totalSum += value; }
        long lowerCount = 0;
        double lowerSum = 0, bestVariance = -1;
        int best = 0;
        for (int t = 0; t < 256; t++)
        {
            lowerCount += histogram[t]; lowerSum += t * (double)histogram[t];
            long upperCount = pixels.Length - lowerCount;
            if (lowerCount == 0 || upperCount == 0) continue;
            double difference = lowerSum / lowerCount - (totalSum - lowerSum) / upperCount;
            double variance = (double)lowerCount * upperCount * difference * difference;
            if (variance > bestVariance) { bestVariance = variance; best = t; }
        }
        return bestVariance < 0 ? pixels[0] : best;
    }
}
