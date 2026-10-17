namespace Binarization.Core;

public sealed record BinarizationSettings(int Threshold = 128, int Window = 25,
    double NiblackK = -0.2, double SauvolaK = 0.5, double PhansalkarK = 0.25);

public sealed record BinarizationResult(byte[] Threshold, byte[] Niblack, byte[] Sauvola,
    byte[] Phansalkar, byte[] Otsu, int OtsuThreshold);

public static class Binarizer
{
    // Retain fractional luminance for statistics and threshold comparisons.
    public static double[] ToGrayscale(byte[] bgra)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (bgra.Length % 4 != 0) throw new ArgumentException("Expected BGRA pixels.", nameof(bgra));
        var gray = new double[bgra.Length / 4];
        for (int i = 0; i < gray.Length; i++)
        {
            int offset = i * 4;
            gray[i] = 0.299 * bgra[offset + 2] + 0.587 * bgra[offset + 1] + 0.114 * bgra[offset];
        }
        return gray;
    }

    public static BinarizationResult Process(double[] gray, int width, int height,
        BinarizationSettings settings, CancellationToken cancellationToken = default)
    {
        ValidateInput(gray, width, height, settings);
        var statistics = BuildImageStatistics(gray, width, height, cancellationToken);
        var (otsu, otsuThreshold) = ApplyOtsu(gray, width, statistics.Histogram, cancellationToken);

        return new BinarizationResult(
            ApplyThreshold(gray, width, settings.Threshold, cancellationToken),
            ApplyNiblack(gray, width, height, settings, statistics, cancellationToken),
            ApplySauvola(gray, width, height, settings, statistics, cancellationToken),
            ApplyPhansalkar(gray, width, height, settings, statistics, cancellationToken),
            otsu,
            otsuThreshold);
    }

    private static void ValidateInput(double[] gray, int width, int height, BinarizationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(gray);
        ArgumentNullException.ThrowIfNull(settings);
        if (width <= 0 || height <= 0 || (long)width * height != gray.Length)
            throw new ArgumentException("Dimensions must match a nonempty image.");

        ValidateSettings(settings);
    }

    private static void ValidateSettings(BinarizationSettings settings)
    {
        if (settings.Threshold is < 0 or > 255 || settings.Window is < 3 or > 101 || settings.Window % 2 == 0
            || !double.IsFinite(settings.NiblackK) || settings.NiblackK is < -1 or > 0.5
            || !double.IsFinite(settings.SauvolaK) || settings.SauvolaK is < 0 or > 1
            || !double.IsFinite(settings.PhansalkarK) || settings.PhansalkarK is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings));
    }

    private static ImageStatistics BuildImageStatistics(double[] gray, int width, int height,
        CancellationToken cancellationToken)
    {
        int stride = checked(width + 1);
        var sums = new double[checked(stride * (height + 1))];
        var squares = new double[sums.Length];
        var histogram = new int[256];
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int rowOffset = y * width;
            int integralRowOffset = (y + 1) * stride;
            double rowSum = 0, rowSquareSum = 0;
            for (int x = 0; x < width; x++)
            {
                double value = gray[rowOffset + x];
                if (!double.IsFinite(value) || value is < 0 or > 255)
                    throw new ArgumentException("Grayscale values must be between 0 and 255.", nameof(gray));
                histogram[(int)value]++;
                rowSum += value;
                rowSquareSum += value * value;
                int index = integralRowOffset + x + 1;
                sums[index] = sums[index - stride] + rowSum;
                squares[index] = squares[index - stride] + rowSquareSum;
            }
        }

        return new ImageStatistics(sums, squares, histogram, stride);
    }

    private static byte[] ApplyThreshold(double[] gray, int width, double threshold,
        CancellationToken cancellationToken)
    {
        var result = new byte[gray.Length];
        for (int rowOffset = 0; rowOffset < gray.Length; rowOffset += width)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                int index = rowOffset + x;
                result[index] = Binary(gray[index], threshold);
            }
        }
        return result;
    }

    private static byte[] ApplyNiblack(double[] gray, int width, int height,
        BinarizationSettings settings, ImageStatistics statistics, CancellationToken cancellationToken) =>
        ApplyLocalThreshold(gray, width, height, settings.Window, statistics,
            (mean, std) => mean + settings.NiblackK * std,
            cancellationToken);

    private static byte[] ApplySauvola(double[] gray, int width, int height,
        BinarizationSettings settings, ImageStatistics statistics, CancellationToken cancellationToken) =>
        ApplyLocalThreshold(gray, width, height, settings.Window, statistics,
            (mean, std) => mean * (1 + settings.SauvolaK * (std / 128 - 1)),
            cancellationToken);

    private static byte[] ApplyPhansalkar(double[] gray, int width, int height,
        BinarizationSettings settings, ImageStatistics statistics, CancellationToken cancellationToken) =>
        ApplyLocalThreshold(gray, width, height, settings.Window, statistics,
            (mean, std) =>
            {
                double normalizedMean = mean / 255;
                return normalizedMean * (1 + 2 * Math.Exp(-10 * normalizedMean)
                    + settings.PhansalkarK * (std / 127.5 - 1));
            },
            cancellationToken, normalize: true);

    private static (byte[] Pixels, int Threshold) ApplyOtsu(double[] gray, int width,
        int[] histogram, CancellationToken cancellationToken)
    {
        int threshold = FindOtsuThreshold(histogram, gray.Length);
        return (ApplyThreshold(gray, width, threshold, cancellationToken), threshold);
    }

    private static byte[] ApplyLocalThreshold(double[] gray, int width, int height, int window,
        ImageStatistics statistics, Func<double, double, double> calculateThreshold,
        CancellationToken cancellationToken, bool normalize = false)
    {
        var result = new byte[gray.Length];
        int radius = window / 2;
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int rowOffset = y * width;
            int top = Math.Max(0, y - radius), bottom = Math.Min(height, y + radius + 1);
            for (int x = 0; x < width; x++)
            {
                int left = Math.Max(0, x - radius), right = Math.Min(width, x + radius + 1);
                int count = (right - left) * (bottom - top);
                double mean = Area(statistics.Sums, left, top, right, bottom, statistics.Stride) / count;
                double meanSquare = Area(statistics.Squares, left, top, right, bottom, statistics.Stride) / count;
                double std = Math.Sqrt(Math.Max(0, meanSquare - mean * mean));
                int index = rowOffset + x;
                double value = normalize ? gray[index] / 255 : gray[index];
                result[index] = Binary(value, calculateThreshold(mean, std));
            }
        }
        return result;
    }

    private static byte Binary(double value, double threshold) => value > threshold ? (byte)255 : (byte)0;

    private static double Area(double[] integral, int left, int top, int right, int bottom, int stride) =>
        integral[bottom * stride + right] - integral[top * stride + right]
        - integral[bottom * stride + left] + integral[top * stride + left];

    private readonly record struct ImageStatistics(double[] Sums, double[] Squares, int[] Histogram, int Stride);

    private static int FindOtsuThreshold(int[] histogram, int count)
    {
        double sum = 0;
        for (int i = 0; i < 256; i++) sum += i * (double)histogram[i];
        int background = 0, threshold = 0;
        double backgroundSum = 0, best = -1;
        for (int t = 0; t < 256; t++)
        {
            background += histogram[t];
            if (background == 0) continue;
            int foreground = count - background;
            if (foreground == 0) break;
            backgroundSum += t * (double)histogram[t];
            double difference = backgroundSum / background - (sum - backgroundSum) / foreground;
            double variance = (double)background * foreground * difference * difference;
            if (variance > best) { best = variance; threshold = t; }
        }
        return threshold;
    }
}
