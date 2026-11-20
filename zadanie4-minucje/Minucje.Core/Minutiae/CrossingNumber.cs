using Minucje.Core.Imaging;
using Minucje.Core.Models;

namespace Minucje.Core.Minutiae;

// Crossing Number (Rutovitz). Dla piksela szkieletu P idziemy dookoła po sąsiadach P1..P8
// i liczymy, ile razy wartość zmienia się między 0 a 1:
//
//   CN = ½ · Σ |Pi − Pi+1|,  i = 1..8,  P9 = P1
//
// Każda linia wychodząca z P daje dwie zmiany (wejście 0→1 i wyjście 1→0),
// więc CN to po prostu liczba linii wychodzących z piksela:
//
//   CN = 0  punkt izolowany      CN = 1  zakończenie
//   CN = 2  zwykły punkt linii   CN = 3  rozwidlenie      CN = 4  skrzyżowanie
public static class CrossingNumber
{
    // minucje tego samego typu bliżej niż tyle pikseli to jedna minucja
    private const int MergeDistance = 2;

    public static int Compute(byte[] skeleton, int pixelIndex, int paddedWidth)
    {
        int[] neighbours = Neighbourhood.GetClockwise(skeleton, pixelIndex, paddedWidth);

        int transitions = 0;
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8; // po P8 wracamy do P1
            transitions += Math.Abs(neighbours[i] - neighbours[next]);
        }
        return transitions / 2;
    }

    // CN -> typ minucji; CN = 2 to zwykły punkt linii, więc nie jest minucją (null)
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

    // liczy CN dla każdego piksela szkieletu i zbiera te, które są minucjami
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

                // współrzędne w obrazie bez ramki
                int x = paddedX - 1;
                int y = paddedY - 1;

                // w miejscu rozwidlenia CN = 3 mogą mieć 2-3 sąsiednie piksele - liczymy je jako jedną minucję
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
