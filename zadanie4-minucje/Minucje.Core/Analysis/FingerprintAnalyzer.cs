using Minucje.Core.Minutiae;
using Minucje.Core.Models;
using Minucje.Core.Preprocessing;
using Minucje.Core.Thinning;

namespace Minucje.Core.Analysis;

public static class FingerprintAnalyzer
{
    public static FingerprintAnalysis Analyze(byte[] grayscale, int width, int height, AnalysisOptions options)
    {
        int paddedWidth = width + 2;
        int paddedHeight = height + 2;

        bool[] mask = FingerprintMask.Compute(grayscale, width, height);
        byte[] binaryImage = Binarization.Apply(grayscale, width, height, mask);
        byte[] skeleton = K3M.Thin(binaryImage, paddedWidth, paddedHeight);
        List<Minutia> detected = CrossingNumber.FindMinutiae(skeleton, paddedWidth, paddedHeight);

        var rejected = new List<RejectedMinutia>();
        if (options.RemoveFalseMinutiae)
        {
            rejected = MinutiaeFilter.Apply(detected, mask, width, height, options.BorderMargin, options.MinDistance);
        }

        var rejectedMinutiae = rejected.Select(item => item.Minutia).ToHashSet();
        List<Minutia> realMinutiae = detected.Where(minutia => !rejectedMinutiae.Contains(minutia)).ToList();

        return new FingerprintAnalysis
        {
            Width = width,
            Height = height,
            Mask = mask,
            Binary = binaryImage,
            Skeleton = skeleton,
            Detected = detected,
            Minutiae = realMinutiae,
            Rejected = rejected,
        };
    }
}
