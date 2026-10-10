namespace Binarization.Core;

public sealed class ThresholdBinarizer : BinarizationAlgorithm
{
    public override Algorithm Algorithm => Algorithm.Threshold;

    protected override byte[] Process(GrayImage image, Parameters parameters, out int? globalThreshold)
    {
        globalThreshold = parameters.Threshold;
        return ApplyThreshold(image.Pixels.Span, parameters.Threshold);
    }
}
