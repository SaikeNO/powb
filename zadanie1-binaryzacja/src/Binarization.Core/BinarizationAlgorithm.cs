using System.Diagnostics;

namespace Binarization.Core;

/// <summary>Shared validation and timing for independently usable algorithms.</summary>
public abstract class BinarizationAlgorithm
{
    public abstract Algorithm Algorithm { get; }

    public BinarizationResult Run(GrayImage image, Parameters? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var p = parameters ?? new Parameters();
        p.Validate(Algorithm);
        var clock = Stopwatch.StartNew();
        byte[] output = Process(image, p, out int? threshold);
        var resultImage = new GrayImage(image.Width, image.Height, output);
        clock.Stop();
        return new(resultImage, clock.Elapsed, threshold);
    }

    protected abstract byte[] Process(GrayImage image, Parameters parameters, out int? globalThreshold);

    protected static byte[] ApplyThreshold(ReadOnlySpan<byte> input, int threshold)
    {
        var output = new byte[input.Length];
        for (int i = 0; i < input.Length; i++)
            output[i] = input[i] > threshold ? (byte)255 : (byte)0;
        return output;
    }
}
