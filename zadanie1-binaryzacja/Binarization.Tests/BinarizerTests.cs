using Binarization.Core;

namespace Binarization.Tests;

public class BinarizerTests
{
    [Fact]
    public void GrayscaleUsesRgbWeightsAndIgnoresAlpha()
    {
        var result = Binarizer.ToGrayscale([0, 0, 255, 0, 0, 255, 0, 128, 255, 0, 0, 255]);
        Assert.Equal(76.245, result[0], 8);
        Assert.Equal(149.685, result[1], 8);
        Assert.Equal(29.07, result[2], 8);
    }

    [Fact]
    public void ThresholdComparisonIsStrictAndKeepsFractionalLuminance()
    {
        var result = Binarizer.Process([0, 128, 128.1, 255], 4, 1, new());
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, result.Threshold);
    }

    [Fact]
    public void OtsuUsesFirstThresholdOnPlateau()
    {
        var result = Binarizer.Process([10, 10, 240, 240], 2, 2, new());
        Assert.Equal(10, result.OtsuThreshold);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, result.Otsu);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(128)]
    [InlineData(255)]
    public void ConstantImageUsesZeroOtsuThreshold(int value)
    {
        var result = Binarizer.Process([value], 1, 1, new());
        Assert.Equal(0, result.OtsuThreshold);
        Assert.Equal(value > 0 ? (byte)255 : (byte)0, result.Otsu[0]);
        Assert.Equal((byte)0, result.Niblack[0]);
    }

    [Theory]
    [InlineData(1, 7, 3)]
    [InlineData(7, 1, 5)]
    [InlineData(7, 6, 3)]
    [InlineData(7, 6, 5)]
    [InlineData(3, 2, 101)]
    public void IntegralStatisticsMatchDirectNeighborhoodCalculation(int width, int height, int window)
    {
        var random = new Random(1729);
        var gray = Enumerable.Range(0, width * height).Select(_ => random.NextDouble() * 255).ToArray();
        var original = (double[])gray.Clone();
        var settings = new BinarizationSettings(Window: window, NiblackK: -0.35, SauvolaK: 0.65, PhansalkarK: 0.15);
        var result = Binarizer.Process(gray, width, height, settings);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            var values = new List<double>();
            for (int yy = Math.Max(0, y - window / 2); yy <= Math.Min(height - 1, y + window / 2); yy++)
            for (int xx = Math.Max(0, x - window / 2); xx <= Math.Min(width - 1, x + window / 2); xx++)
                values.Add(gray[yy * width + xx]);
            double mean = values.Average();
            double std = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
            int i = y * width + x;
            Assert.Equal(Bin(gray[i], mean - 0.35 * std), result.Niblack[i]);
            Assert.Equal(Bin(gray[i], mean * (1 + 0.65 * (std / 128 - 1))), result.Sauvola[i]);
            double m = mean / 255;
            Assert.Equal(Bin(gray[i] / 255, m * (1 + 2 * Math.Exp(-10 * m) + 0.15 * (std / 127.5 - 1))), result.Phansalkar[i]);
        }
        Assert.Equal(original, gray);
    }

    [Fact]
    public void RejectsInvalidInputAndSupportsCancellation()
    {
        Assert.Throws<ArgumentException>(() => Binarizer.Process([0], 2, 1, new()));
        Assert.Throws<ArgumentException>(() => Binarizer.Process([double.NaN], 1, 1, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Binarizer.Process([0], 1, 1, new(Window: 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Binarizer.Process([0], 1, 1, new(SauvolaK: double.NaN)));
        Assert.Throws<OperationCanceledException>(() => Binarizer.Process([0], 1, 1, new(), new CancellationToken(true)));
    }

    private static byte Bin(double value, double threshold) => value > threshold ? (byte)255 : (byte)0;
}
