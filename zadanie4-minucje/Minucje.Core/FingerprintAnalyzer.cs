namespace Minucje.Core;

public sealed record AnalysisOptions(
    bool RemoveFalseMinutiae = true,
    int BorderMargin = 16,
    int MinDistance = 8);

// wszystko, co program pokazuje dla jednego odcisku
public sealed class FingerprintAnalysis
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required bool[] Mask { get; init; }          // width × height, true = odcisk

    public required byte[] Binary { get; init; }        // 0/1 z ramką 1 piksela

    public required byte[] Skeleton { get; init; }      // 0/1 z ramką 1 piksela

    public required List<Minutia> Detected { get; init; }       // wszystko, co znalazł Crossing Number

    public required List<Minutia> Minutiae { get; init; }       // minucje po odrzuceniu fałszywych

    public required List<RejectedMinutia> Rejected { get; init; }

    public int Count(MinutiaType type)
    {
        return Minutiae.Count(minutia => minutia.Type == type);
    }
}

// Cała ekstrakcja cech w jednym miejscu:
// maska -> binaryzacja -> szkielet (K3M) -> Crossing Number -> usunięcie fałszywych minucji
public static class FingerprintAnalyzer
{
    public static FingerprintAnalysis Analyze(byte[] grayscale, int width, int height, AnalysisOptions options)
    {
        int paddedWidth = width + 2;
        int paddedHeight = height + 2;

        bool[] mask = Preprocessing.ComputeMask(grayscale, width, height);
        byte[] binary = Preprocessing.Binarize(grayscale, width, height, mask);
        byte[] skeleton = K3M.Thin(binary, paddedWidth, paddedHeight);
        List<Minutia> detected = CrossingNumber.FindMinutiae(skeleton, paddedWidth, paddedHeight);

        var rejected = new List<RejectedMinutia>();
        if (options.RemoveFalseMinutiae)
        {
            rejected = MinutiaeFilter.Apply(detected, mask, width, height, options.BorderMargin, options.MinDistance);
        }

        List<Minutia> minutiae = detected
            .Where(minutia => !rejected.Any(item => item.Minutia == minutia))
            .ToList();

        return new FingerprintAnalysis
        {
            Width = width,
            Height = height,
            Mask = mask,
            Binary = binary,
            Skeleton = skeleton,
            Detected = detected,
            Minutiae = minutiae,
            Rejected = rejected,
        };
    }
}
