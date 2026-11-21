using Minucje.Core.Minutiae;
using Minucje.Core.Models;
using Minucje.Core.Preprocessing;
using Minucje.Core.Thinning;

namespace Minucje.Core.Analysis;

// cały algorytm w jednym miejscu - od tego pliku najlepiej zacząć omawianie
public static class FingerprintAnalyzer
{
    public static FingerprintAnalysis Analyze(byte[] grayscale, int width, int height, AnalysisOptions options)
    {
        int paddedWidth = width + 2;
        int paddedHeight = height + 2;

        // 1. maska: gdzie jest odcisk, a gdzie tło skanera
        bool[] mask = FingerprintMask.Compute(grayscale, width, height);
        // 2. binaryzacja: linia = 1, tło = 0
        byte[] binaryImage = Binarization.Apply(grayscale, width, height, mask);
        // 3. szkieletyzacja K3M: linie grubości 1 piksela
        byte[] skeleton = K3M.Thin(binaryImage, paddedWidth, paddedHeight);
        // 4. Crossing Number: dla każdego piksela szkieletu liczymy CN -> minucje
        List<Minutia> detected = CrossingNumber.FindMinutiae(skeleton, paddedWidth, paddedHeight);

        // 5. usuwanie fałszywych minucji (brzeg skanera, pary blisko siebie)
        var rejected = new List<RejectedMinutia>();
        if (options.RemoveFalseMinutiae)
        {
            rejected = MinutiaeFilter.Apply(detected, mask, width, height, options.BorderMargin, options.MinDistance);
        }

        // prawdziwe minucje = wszystkie wykryte minus odrzucone
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
