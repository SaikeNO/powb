namespace Binarization.Core;

public sealed class OtsuBinarizer : BinarizationAlgorithm
{
    public override Algorithm Algorithm => Algorithm.Otsu;

    protected override byte[] Process(GrayImage image, Parameters parameters, out int? globalThreshold)
    {
        int threshold = Otsu(image.Pixels.Span);
        globalThreshold = threshold;
        return ApplyThreshold(image.Pixels.Span, threshold);
    }

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
