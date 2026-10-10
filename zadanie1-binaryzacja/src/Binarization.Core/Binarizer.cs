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
        BinarizationAlgorithm implementation = algorithm switch
        {
            Algorithm.Threshold => new ThresholdBinarizer(),
            Algorithm.Niblack => new NiblackBinarizer(),
            Algorithm.Sauvola => new SauvolaBinarizer(),
            Algorithm.Phansalkar => new PhansalkarBinarizer(),
            Algorithm.Otsu => new OtsuBinarizer(),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm))
        };
        return implementation.Run(image, parameters);
    }
}
