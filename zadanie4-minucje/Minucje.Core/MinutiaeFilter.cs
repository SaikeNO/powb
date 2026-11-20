namespace Minucje.Core;

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
        List<Minutia> minutiae, bool[] mask, int width, int height, int borderMargin, int minDistance)
    {
        var rejected = new List<RejectedMinutia>();
        var remaining = new List<Minutia>();

        // 1. brzeg skanera
        foreach (Minutia minutia in minutiae)
        {
            if (IsNearBorder(minutia, mask, width, height, borderMargin))
            {
                rejected.Add(new RejectedMinutia(minutia, RejectionReason.ScannerBorder));
            }
            else
            {
                remaining.Add(minutia);
            }
        }

        // 2. pary minucji blisko siebie
        foreach (Minutia a in remaining)
        {
            foreach (Minutia b in remaining)
            {
                if (a == b || Distance(a, b) >= minDistance)
                {
                    continue;
                }

                RejectionReason? reason = GetPairReason(a.Type, b.Type);
                bool alreadyRejected = rejected.Any(item => item.Minutia == a);
                if (reason != null && !alreadyRejected)
                {
                    rejected.Add(new RejectedMinutia(a, reason.Value));
                }
            }
        }

        return rejected;
    }

    // czy w kwadracie o promieniu margin wokół minucji jest piksel spoza odcisku albo spoza obrazu
    public static bool IsNearBorder(Minutia minutia, bool[] mask, int width, int height, int margin)
    {
        for (int y = minutia.Y - margin; y <= minutia.Y + margin; y++)
        {
            for (int x = minutia.X - margin; x <= minutia.X + margin; x++)
            {
                bool outsideImage = x < 0 || y < 0 || x >= width || y >= height;
                if (outsideImage || !mask[y * width + x])
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static double Distance(Minutia a, Minutia b)
    {
        int dx = a.X - b.X;
        int dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static RejectionReason? GetPairReason(MinutiaType a, MinutiaType b)
    {
        if (a == MinutiaType.Ending && b == MinutiaType.Ending)
        {
            return RejectionReason.BrokenRidge;
        }
        if (a == MinutiaType.Bifurcation && b == MinutiaType.Bifurcation)
        {
            return RejectionReason.Bridge;
        }
        if (a == MinutiaType.Ending || b == MinutiaType.Ending)
        {
            if (a == MinutiaType.Bifurcation || b == MinutiaType.Bifurcation)
            {
                return RejectionReason.Spur;
            }
        }
        return null; // pary ze skrzyżowaniem lub punktem izolowanym zostawiamy
    }
}
