using Minucje.Core.Models;

namespace Minucje.Core.Minutiae;

// Usuwanie fałszywych minucji. Zwraca listę odrzuconych minucji razem z przyczyną.
//
//  1. Brzeg skanera: na granicy odcisku linie papilarne są ucięte, więc każda linia dochodząca do brzegu
//     daje fałszywe zakończenie. Odrzucamy minucje, które mają tło skanera (albo krawędź obrazu)
//     w odległości mniejszej niż borderMargin.
//
//  2. Dwie minucje bliżej siebie niż minDistance (mniej niż odstęp między liniami) to prawie zawsze szum:
//       zakończenie + zakończenie  -> przerwana linia,
//       zakończenie + rozwidlenie  -> ostroga (krótkie fałszywe odgałęzienie),
//       rozwidlenie + rozwidlenie  -> mostek między liniami albo dziura w linii.
//     Odrzucamy obie minucje z takiej pary.
public static class MinutiaeFilter
{
    public static List<RejectedMinutia> Apply(
        List<Minutia> minutiae, bool[] fingerprintMask, int width, int height, int borderMargin, int minDistance)
    {
        var rejected = new List<RejectedMinutia>();
        var insideFingerprint = new List<Minutia>();

        // 1. brzeg skanera
        foreach (Minutia minutia in minutiae)
        {
            if (IsNearBorder(minutia, fingerprintMask, width, height, borderMargin))
            {
                rejected.Add(new RejectedMinutia(minutia, RejectionReason.ScannerBorder));
            }
            else
            {
                insideFingerprint.Add(minutia);
            }
        }

        // 2. pary minucji blisko siebie (każda minucja odrzucona najwyżej raz)
        var rejectedInPairs = new HashSet<Minutia>();
        foreach (Minutia minutia in insideFingerprint)
        {
            foreach (Minutia other in insideFingerprint)
            {
                if (minutia == other || rejectedInPairs.Contains(minutia) || Distance(minutia, other) >= minDistance)
                {
                    continue;
                }

                RejectionReason? reason = GetPairReason(minutia.Type, other.Type);
                if (reason != null)
                {
                    rejected.Add(new RejectedMinutia(minutia, reason.Value));
                    rejectedInPairs.Add(minutia);
                }
            }
        }

        return rejected;
    }

    // czy w kwadracie o promieniu margin wokół minucji jest piksel spoza odcisku albo spoza obrazu
    public static bool IsNearBorder(Minutia minutia, bool[] fingerprintMask, int width, int height, int margin)
    {
        for (int y = minutia.Y - margin; y <= minutia.Y + margin; y++)
        {
            for (int x = minutia.X - margin; x <= minutia.X + margin; x++)
            {
                bool outsideImage = x < 0 || y < 0 || x >= width || y >= height;
                if (outsideImage || !fingerprintMask[y * width + x])
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static double Distance(Minutia first, Minutia second)
    {
        int dx = first.X - second.X;
        int dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static RejectionReason? GetPairReason(MinutiaType first, MinutiaType second)
    {
        bool bothEndings = first == MinutiaType.Ending && second == MinutiaType.Ending;
        bool bothBifurcations = first == MinutiaType.Bifurcation && second == MinutiaType.Bifurcation;
        bool endingAndBifurcation =
            (first == MinutiaType.Ending && second == MinutiaType.Bifurcation) ||
            (first == MinutiaType.Bifurcation && second == MinutiaType.Ending);

        if (bothEndings)
        {
            return RejectionReason.BrokenRidge;
        }
        if (bothBifurcations)
        {
            return RejectionReason.Bridge;
        }
        if (endingAndBifurcation)
        {
            return RejectionReason.Spur;
        }
        return null; // pary ze skrzyżowaniem lub punktem izolowanym zostawiamy
    }
}
