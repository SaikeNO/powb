using Minucje.Core.Imaging;
using Minucje.Core.Models;

namespace Minucje.Core.Minutiae;

// GŁÓWNA CZĘŚĆ ZADANIA
// CN = 1/2 * suma |Pi - Pi+1| po 8 sąsiadach dookoła (P9 = P1)
// = liczba linii wychodzących z piksela: 0 punkt, 1 zakończenie, 2 zwykła linia, 3 rozwidlenie, 4 skrzyżowanie
public static class CrossingNumber
{
    private const int MergeDistance = 2;   // minucje tego samego typu bliżej niż 2 px liczymy jako jedną

    public static int Compute(byte[] skeleton, int pixelIndex, int paddedWidth)
    {
        int[] neighbours = Neighbourhood.GetClockwise(skeleton, pixelIndex, paddedWidth);

        // liczymy zmiany 0->1 i 1->0 idąc dookoła piksela
        int transitions = 0;
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;   // po P8 wracamy do P1
            transitions += Math.Abs(neighbours[i] - neighbours[next]);
        }
        return transitions / 2;   // każda linia daje 2 zmiany (wejście i wyjście)
    }

    // CN -> typ minucji; CN = 2 to zwykły punkt linii, więc null
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

    // przechodzimy po wszystkich pikselach szkieletu i zbieramy te, które są minucjami
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

                // współrzędne bez ramki
                int x = paddedX - 1;
                int y = paddedY - 1;

                // przy rozwidleniu CN = 3 może mieć kilka sąsiednich pikseli - liczymy je raz
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
