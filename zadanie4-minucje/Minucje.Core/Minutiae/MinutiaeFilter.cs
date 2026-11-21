using Minucje.Core.Models;

namespace Minucje.Core.Minutiae;

public static class MinutiaeFilter
{
    public static List<RejectedMinutia> Apply(
        List<Minutia> minutiae, bool[] fingerprintMask, int width, int height, int borderMargin, int minDistance)
    {
        var rejected = new List<RejectedMinutia>();
        var insideFingerprint = new List<Minutia>();

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
        return null;
    }
}
