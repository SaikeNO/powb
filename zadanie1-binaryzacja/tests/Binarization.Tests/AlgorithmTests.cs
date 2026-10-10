using Binarization.Core;
namespace Binarization.Tests;

public class AlgorithmTests
{
    [Fact]
    public void ThresholdBoundaryIsStrict()
    {
        var r = Binarizer.Run(new(5, 1, [0, 127, 128, 129, 255]), Algorithm.Threshold);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 255 }, r.Image.Pixels.ToArray());
        Assert.Equal(128, r.GlobalThreshold);
    }
    [Fact]
    public void OtsuUsesLowestTieAndSeparatesModes()
    {
        var r = Binarizer.Run(new(6, 1, [20, 20, 20, 200, 200, 200]), Algorithm.Otsu);
        Assert.Equal(20, r.GlobalThreshold);
        Assert.Equal(new byte[] { 0, 0, 0, 255, 255, 255 }, r.Image.Pixels.ToArray());
    }
    [Theory]
    [InlineData(0)] [InlineData(73)] [InlineData(255)]
    public void OtsuConstantImage(int value)
    {
        var r = Binarizer.Run(new(1, 1, [(byte)value]), Algorithm.Otsu);
        Assert.Equal(value, r.GlobalThreshold); Assert.Equal((byte)0, r.Image.Pixels.Span[0]);
    }
    [Fact]
    public void OtsuMatchesExhaustiveWeightedVariance()
    {
        byte[] data = [3, 10, 12, 13, 110, 140, 150, 220, 250];
        int best = 0; double score = double.PositiveInfinity;
        for (int t = 0; t < 255; t++)
        {
            var lower = data.Where(v => v <= t).Select(v => (double)v).ToArray();
            var upper = data.Where(v => v > t).Select(v => (double)v).ToArray();
            if (lower.Length == 0 || upper.Length == 0) continue;
            double lm = lower.Average(), um = upper.Average();
            double variance = lower.Sum(v => (v - lm) * (v - lm)) + upper.Sum(v => (v - um) * (v - um));
            if (variance < score) { best = t; score = variance; }
        }
        Assert.Equal(best, Binarizer.Run(new(data.Length, 1, data), Algorithm.Otsu).GlobalThreshold);
    }
    [Theory]
    [InlineData(Algorithm.Niblack, 1)] [InlineData(Algorithm.Niblack, 3)] [InlineData(Algorithm.Niblack, 25)]
    [InlineData(Algorithm.Sauvola, 1)] [InlineData(Algorithm.Sauvola, 3)] [InlineData(Algorithm.Sauvola, 25)]
    [InlineData(Algorithm.Phansalkar, 1)] [InlineData(Algorithm.Phansalkar, 3)] [InlineData(Algorithm.Phansalkar, 25)]
    public void IntegralLocalMethodsMatchNaiveReference(Algorithm algorithm, int window)
    {
        var random = new Random(1234);
        var data = new byte[7 * 5]; random.NextBytes(data);
        var p = new Parameters(WindowSize: window, NiblackK: -0.31, SauvolaK: 0.37, SauvolaR: 100,
            PhansalkarK: 0.28, PhansalkarR: 0.6, P: 1.8, Q: 8);
        var image = new GrayImage(7, 5, data);
        var r = Binarizer.Run(image, algorithm, p);
        Assert.Equal(Naive(image, algorithm, p), r.Image.Pixels.ToArray());
        Assert.Null(r.GlobalThreshold); Assert.All(r.Image.Pixels.ToArray(), v => Assert.True(v is 0 or 255));
    }
    [Theory]
    [InlineData(Algorithm.Niblack, 0)] [InlineData(Algorithm.Niblack, 100)]
    [InlineData(Algorithm.Sauvola, 0)] [InlineData(Algorithm.Sauvola, 100)]
    [InlineData(Algorithm.Phansalkar, 0)] [InlineData(Algorithm.Phansalkar, 100)]
    public void OnePixelAndZeroVarianceMatchReference(Algorithm algorithm, int value)
    {
        var image = new GrayImage(1, 1, [(byte)value]);
        Assert.Equal(Naive(image, algorithm, new()), Binarizer.Run(image, algorithm).Image.Pixels.ToArray());
    }
    [Fact]
    public void PhansalkarUsesNormalizedMean()
    {
        var result = Binarizer.Run(new(1, 1, [20]), Algorithm.Phansalkar);
        // Normalized mean yields threshold above 20; using raw mean would incorrectly yield white.
        Assert.Equal((byte)0, result.Image.Pixels.Span[0]);
    }
    [Fact]
    public void InvalidParametersAreRejected()
    {
        var image = new GrayImage(1, 1, [100]);
        foreach (int window in new[] { 0, -1, 2 })
            Assert.Throws<ArgumentException>(() => Binarizer.Run(image, Algorithm.Niblack, new(WindowSize: window)));
        Assert.Throws<ArgumentException>(() => Binarizer.Run(image, Algorithm.Threshold, new(Threshold: 256)));
        Assert.Throws<ArgumentException>(() => Binarizer.Run(image, Algorithm.Sauvola, new(SauvolaR: 0)));
        Assert.Throws<ArgumentException>(() => Binarizer.Run(image, Algorithm.Phansalkar, new(PhansalkarR: -1)));
        Assert.Throws<ArgumentException>(() => Binarizer.Run(image, Algorithm.Niblack, new(NiblackK: double.NaN)));
        Assert.Throws<ArgumentException>(() => Binarizer.Run(image, Algorithm.Phansalkar, new(Q: double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Binarizer.Run(image, (Algorithm)99));
        Assert.Throws<ArgumentException>(() => new GrayImage(0, 1, []));
        Assert.Throws<ArgumentException>(() => new GrayImage(2, 2, [0]));
    }
    [Fact]
    public void ImageOwnsInputBuffer()
    {
        byte[] data = [40]; var image = new GrayImage(1, 1, data); data[0] = 200;
        Assert.Equal((byte)40, image.Pixels.Span[0]);
    }
    private static byte[] Naive(GrayImage image, Algorithm a, Parameters p)
    {
        var data = image.Pixels.ToArray(); var output = new byte[data.Length]; int radius = p.WindowSize / 2;
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            var local = new List<double>();
            for (int yy = Math.Max(0, y - radius); yy <= Math.Min(image.Height - 1, y + radius); yy++)
            for (int xx = Math.Max(0, x - radius); xx <= Math.Min(image.Width - 1, x + radius); xx++)
                local.Add(data[yy * image.Width + xx] / (a == Algorithm.Phansalkar ? 255.0 : 1));
            double mean = local.Average();
            double sd = Math.Sqrt(local.Average(v => (v - mean) * (v - mean)));
            double threshold = a switch
            {
                Algorithm.Niblack => mean + p.NiblackK * sd,
                Algorithm.Sauvola => mean * (1 + p.SauvolaK * (sd / p.SauvolaR - 1)),
                _ => mean * (1 + p.P * Math.Exp(-p.Q * mean) + p.PhansalkarK * (sd / p.PhansalkarR - 1))
            };
            double value = data[y * image.Width + x] / (a == Algorithm.Phansalkar ? 255.0 : 1);
            output[y * image.Width + x] = value > threshold ? (byte)255 : (byte)0;
        }
        return output;
    }
}
