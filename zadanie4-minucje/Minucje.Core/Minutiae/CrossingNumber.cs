using Minucje.Core.Imaging;
using Minucje.Core.Models;

namespace Minucje.Core.Minutiae;

public static class CrossingNumber
{
    private const int MergeDistance = 2;

    public static int Compute(byte[] skeleton, int pixelIndex, int paddedWidth)
    {
        int[] neighbours = Neighbourhood.GetClockwise(skeleton, pixelIndex, paddedWidth);

        int transitions = 0;
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            transitions += Math.Abs(neighbours[i] - neighbours[next]);
        }
        return transitions / 2;
    }

    public static MinutiaType? Classify(int crossingNumber)
    {
        switch (crossingNumber)
        {
            case 0:
                return MinutiaType.Isolated;
            case 1:
                return MinutiaType.Ending;
            case 3:
                return MinutiaType.Bifurcation;
            case 4:
                return MinutiaType.Crossing;
            default:
                return null;
        }
    }

    public static List<Minutia> FindMinutiae(byte[] skeleton, int paddedWidth, int paddedHeight)
    {
        var minutiae = new List<Minutia>();
        for (int paddedY = 1; paddedY < paddedHeight - 1; paddedY++)
        {
            for (int paddedX = 1; paddedX < paddedWidth - 1; paddedX++)
            {
                int pixelIndex = paddedY * paddedWidth + paddedX;
                if (skeleton[pixelIndex] == 0)
                {
                    continue;
                }

                int crossingNumber = Compute(skeleton, pixelIndex, paddedWidth);
                MinutiaType? type = Classify(crossingNumber);
                if (type == null)
                {
                    continue;
                }

                int x = paddedX - 1;
                int y = paddedY - 1;

                bool alreadyFound = minutiae.Any(found =>
                    found.Type == type && Math.Abs(found.X - x) <= MergeDistance && Math.Abs(found.Y - y) <= MergeDistance);
                if (!alreadyFound)
                {
                    minutiae.Add(new Minutia(x, y, type.Value, crossingNumber));
                }
            }
        }
        return minutiae;
    }
}
