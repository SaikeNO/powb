namespace Binarization.Core;

public sealed record BinarizationSettings(
    int Threshold = 128,
    int Window = 25,
    double NiblackK = -0.2,
    double SauvolaK = 0.5,
    double PhansalkarK = 0.25);

public sealed record BinarizationResult(
    byte[] Threshold,
    byte[] Niblack,
    byte[] Sauvola,
    byte[] Phansalkar,
    byte[] Otsu,
    int OtsuThreshold);

public static class Binarizer
{
    // Retain fractional luminance for statistics and threshold comparisons.
    public static double[] ToGrayscale(byte[] bgraPixels)
    {
        ArgumentNullException.ThrowIfNull(bgraPixels);

        if (bgraPixels.Length % 4 != 0)
        {
            throw new ArgumentException("Expected BGRA pixels.", nameof(bgraPixels));
        }

        var grayscalePixels = new double[bgraPixels.Length / 4];

        for (int pixelIndex = 0; pixelIndex < grayscalePixels.Length; pixelIndex++)
        {
            int pixelByteOffset = pixelIndex * 4;
            grayscalePixels[pixelIndex] = 0.299 * bgraPixels[pixelByteOffset + 2]
                + 0.587 * bgraPixels[pixelByteOffset + 1]
                + 0.114 * bgraPixels[pixelByteOffset];
        }

        return grayscalePixels;
    }

    public static BinarizationResult Process(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        BinarizationSettings binarizationSettings,
        CancellationToken cancellationToken = default)
    {
        ValidateInput(grayscalePixels, imageWidth, imageHeight, binarizationSettings);

        var imageStatistics = BuildImageStatistics(grayscalePixels, imageWidth, imageHeight, cancellationToken);
        var (otsuPixels, otsuThreshold) = ApplyOtsu(
            grayscalePixels, imageWidth, imageStatistics.BrightnessHistogram, cancellationToken);

        return new BinarizationResult(
            ApplyThreshold(
                grayscalePixels,
                imageWidth,
                binarizationSettings.Threshold,
                cancellationToken),
            ApplyNiblack(
                grayscalePixels,
                imageWidth,
                imageHeight,
                binarizationSettings,
                imageStatistics,
                cancellationToken),
            ApplySauvola(
                grayscalePixels,
                imageWidth,
                imageHeight,
                binarizationSettings,
                imageStatistics,
                cancellationToken),
            ApplyPhansalkar(
                grayscalePixels,
                imageWidth,
                imageHeight,
                binarizationSettings,
                imageStatistics,
                cancellationToken),
            otsuPixels,
            otsuThreshold);
    }

    private static void ValidateInput(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        BinarizationSettings binarizationSettings)
    {
        ArgumentNullException.ThrowIfNull(grayscalePixels);
        ArgumentNullException.ThrowIfNull(binarizationSettings);

        if (imageWidth <= 0
            || imageHeight <= 0
            || (long)imageWidth * imageHeight != grayscalePixels.Length)
        {
            throw new ArgumentException("Dimensions must match a nonempty image.");
        }

        ValidateSettings(binarizationSettings);
    }

    private static void ValidateSettings(BinarizationSettings binarizationSettings)
    {
        if (binarizationSettings.Threshold is < 0 or > 255
            || binarizationSettings.Window is < 3 or > 101 || binarizationSettings.Window % 2 == 0
            || !double.IsFinite(binarizationSettings.NiblackK) || binarizationSettings.NiblackK is < -1 or > 0.5
            || !double.IsFinite(binarizationSettings.SauvolaK) || binarizationSettings.SauvolaK is < 0 or > 1
            || !double.IsFinite(binarizationSettings.PhansalkarK) || binarizationSettings.PhansalkarK is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(binarizationSettings));
        }
    }

    private static ImageStatistics BuildImageStatistics(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        CancellationToken cancellationToken)
    {
        int integralTableWidth = checked(imageWidth + 1);
        var brightnessSumTable = new double[checked(integralTableWidth * (imageHeight + 1))];
        var squaredBrightnessSumTable = new double[brightnessSumTable.Length];
        var brightnessHistogram = new int[256];

        for (int pixelRow = 0; pixelRow < imageHeight; pixelRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int imageRowStartIndex = pixelRow * imageWidth;
            int integralRowStartIndex = (pixelRow + 1) * integralTableWidth;
            double rowBrightnessSum = 0;
            double rowSquaredBrightnessSum = 0;

            for (int pixelColumn = 0; pixelColumn < imageWidth; pixelColumn++)
            {
                double pixelBrightness = grayscalePixels[imageRowStartIndex + pixelColumn];

                if (!double.IsFinite(pixelBrightness) || pixelBrightness is < 0 or > 255)
                {
                    throw new ArgumentException(
                        "Grayscale values must be between 0 and 255.",
                        nameof(grayscalePixels));
                }

                brightnessHistogram[(int)pixelBrightness]++;
                rowBrightnessSum += pixelBrightness;
                rowSquaredBrightnessSum += pixelBrightness * pixelBrightness;

                int integralTableIndex = integralRowStartIndex + pixelColumn + 1;
                brightnessSumTable[integralTableIndex] =
                    brightnessSumTable[integralTableIndex - integralTableWidth] + rowBrightnessSum;
                squaredBrightnessSumTable[integralTableIndex] =
                    squaredBrightnessSumTable[integralTableIndex - integralTableWidth] + rowSquaredBrightnessSum;
            }
        }

        return new ImageStatistics(
            brightnessSumTable,
            squaredBrightnessSumTable,
            brightnessHistogram,
            integralTableWidth);
    }

    private static byte[] ApplyThreshold(
        double[] grayscalePixels,
        int imageWidth,
        double threshold,
        CancellationToken cancellationToken)
    {
        var binaryPixels = new byte[grayscalePixels.Length];

        for (int imageRowStartIndex = 0;
             imageRowStartIndex < grayscalePixels.Length;
             imageRowStartIndex += imageWidth)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int pixelColumn = 0; pixelColumn < imageWidth; pixelColumn++)
            {
                int pixelIndex = imageRowStartIndex + pixelColumn;
                binaryPixels[pixelIndex] = Binary(grayscalePixels[pixelIndex], threshold);
            }
        }

        return binaryPixels;
    }

    private static byte[] ApplyNiblack(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        BinarizationSettings binarizationSettings,
        ImageStatistics imageStatistics,
        CancellationToken cancellationToken) =>
        ApplyLocalThreshold(
            grayscalePixels,
            imageWidth,
            imageHeight,
            binarizationSettings.Window,
            imageStatistics,
            (localMeanBrightness, localStandardDeviation) =>
                localMeanBrightness + binarizationSettings.NiblackK * localStandardDeviation,
            cancellationToken);

    private static byte[] ApplySauvola(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        BinarizationSettings binarizationSettings,
        ImageStatistics imageStatistics,
        CancellationToken cancellationToken) =>
        ApplyLocalThreshold(
            grayscalePixels,
            imageWidth,
            imageHeight,
            binarizationSettings.Window,
            imageStatistics,
            (localMeanBrightness, localStandardDeviation) =>
                localMeanBrightness * (1 + binarizationSettings.SauvolaK * (localStandardDeviation / 128 - 1)),
            cancellationToken);

    private static byte[] ApplyPhansalkar(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        BinarizationSettings binarizationSettings,
        ImageStatistics imageStatistics,
        CancellationToken cancellationToken) =>
        ApplyLocalThreshold(
            grayscalePixels,
            imageWidth,
            imageHeight,
            binarizationSettings.Window,
            imageStatistics,
            (localMeanBrightness, localStandardDeviation) =>
            {
                double normalizedLocalMeanBrightness = localMeanBrightness / 255;
                return normalizedLocalMeanBrightness * (1 + 2 * Math.Exp(-10 * normalizedLocalMeanBrightness)
                    + binarizationSettings.PhansalkarK * (localStandardDeviation / 127.5 - 1));
            },
            cancellationToken,
            normalizePixelBrightness: true);

    private static (byte[] Pixels, int Threshold) ApplyOtsu(
        double[] grayscalePixels,
        int imageWidth,
        int[] brightnessHistogram,
        CancellationToken cancellationToken)
    {
        int threshold = FindOtsuThreshold(brightnessHistogram, grayscalePixels.Length);
        return (ApplyThreshold(grayscalePixels, imageWidth, threshold, cancellationToken), threshold);
    }

    private static byte[] ApplyLocalThreshold(
        double[] grayscalePixels,
        int imageWidth,
        int imageHeight,
        int windowSize,
        ImageStatistics imageStatistics,
        Func<double, double, double> calculateLocalThreshold,
        CancellationToken cancellationToken,
        bool normalizePixelBrightness = false)
    {
        var binaryPixels = new byte[grayscalePixels.Length];
        int windowRadius = windowSize / 2;

        for (int pixelRow = 0; pixelRow < imageHeight; pixelRow++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int imageRowStartIndex = pixelRow * imageWidth;
            int windowTopRow = Math.Max(0, pixelRow - windowRadius);
            int windowBottomRowExclusive = Math.Min(imageHeight, pixelRow + windowRadius + 1);

            for (int pixelColumn = 0; pixelColumn < imageWidth; pixelColumn++)
            {
                int windowLeftColumn = Math.Max(0, pixelColumn - windowRadius);
                int windowRightColumnExclusive = Math.Min(imageWidth, pixelColumn + windowRadius + 1);

                int windowPixelCount = (windowRightColumnExclusive - windowLeftColumn)
                    * (windowBottomRowExclusive - windowTopRow);

                double localMeanBrightness = Area(
                    imageStatistics.BrightnessSumTable,
                    windowLeftColumn, windowTopRow, windowRightColumnExclusive, windowBottomRowExclusive,
                    imageStatistics.IntegralTableWidth) / windowPixelCount;

                double localMeanSquaredBrightness = Area(
                    imageStatistics.SquaredBrightnessSumTable,
                    windowLeftColumn, windowTopRow, windowRightColumnExclusive, windowBottomRowExclusive,
                    imageStatistics.IntegralTableWidth) / windowPixelCount;

                double localStandardDeviation = Math.Sqrt(Math.Max(0,
                    localMeanSquaredBrightness - localMeanBrightness * localMeanBrightness));

                int pixelIndex = imageRowStartIndex + pixelColumn;
                double pixelBrightness = normalizePixelBrightness
                    ? grayscalePixels[pixelIndex] / 255
                    : grayscalePixels[pixelIndex];

                binaryPixels[pixelIndex] = Binary(pixelBrightness,
                    calculateLocalThreshold(localMeanBrightness, localStandardDeviation));
            }
        }

        return binaryPixels;
    }

    private static byte Binary(double pixelBrightness, double threshold) =>
        pixelBrightness > threshold ? (byte)255 : (byte)0;

    private static double Area(
        double[] sumTable,
        int windowLeftColumn,
        int windowTopRow,
        int windowRightColumnExclusive,
        int windowBottomRowExclusive,
        int integralTableWidth) =>
        sumTable[windowBottomRowExclusive * integralTableWidth + windowRightColumnExclusive]
        - sumTable[windowTopRow * integralTableWidth + windowRightColumnExclusive]
        - sumTable[windowBottomRowExclusive * integralTableWidth + windowLeftColumn]
        + sumTable[windowTopRow * integralTableWidth + windowLeftColumn];

    private readonly record struct ImageStatistics(
        double[] BrightnessSumTable,
        double[] SquaredBrightnessSumTable,
        int[] BrightnessHistogram,
        int IntegralTableWidth);

    private static int FindOtsuThreshold(int[] brightnessHistogram, int totalPixelCount)
    {
        double totalBrightnessSum = 0;

        for (int brightnessLevel = 0; brightnessLevel < 256; brightnessLevel++)
        {
            totalBrightnessSum += brightnessLevel * (double)brightnessHistogram[brightnessLevel];
        }

        int backgroundPixelCount = 0;
        int bestThreshold = 0;
        double backgroundBrightnessSum = 0;
        double highestBetweenClassVariance = -1;

        for (int candidateThreshold = 0; candidateThreshold < 256; candidateThreshold++)
        {
            backgroundPixelCount += brightnessHistogram[candidateThreshold];

            if (backgroundPixelCount == 0)
            {
                continue;
            }

            int foregroundPixelCount = totalPixelCount - backgroundPixelCount;

            if (foregroundPixelCount == 0)
            {
                break;
            }

            backgroundBrightnessSum += candidateThreshold * (double)brightnessHistogram[candidateThreshold];
            double classMeanDifference = backgroundBrightnessSum / backgroundPixelCount
                - (totalBrightnessSum - backgroundBrightnessSum) / foregroundPixelCount;
            double betweenClassVariance = (double)backgroundPixelCount * foregroundPixelCount
                * classMeanDifference * classMeanDifference;

            if (betweenClassVariance > highestBetweenClassVariance)
            {
                highestBetweenClassVariance = betweenClassVariance;
                bestThreshold = candidateThreshold;
            }
        }

        return bestThreshold;
    }
}
