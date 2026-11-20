namespace Minucje.Core;

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
    public static int Compute(byte[] skeleton, int index, int paddedWidth)
    {
        int[] p = Neighbourhood.GetClockwise(skeleton, index, paddedWidth);

        int sum = 0;
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8; // po P8 wracamy do P1
            sum += Math.Abs(p[i] - p[next]);
        }
        return sum / 2;
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
        for (int y = 1; y < paddedHeight - 1; y++)
        {
            for (int x = 1; x < paddedWidth - 1; x++)
            {
                int index = y * paddedWidth + x;
                if (skeleton[index] == 0)
                {
                    continue;
                }

                int crossingNumber = Compute(skeleton, index, paddedWidth);
                MinutiaType? type = Classify(crossingNumber);
                if (type == null)
                {
                    continue;
                }

                // w miejscu rozwidlenia CN = 3 mogą mieć 2-3 sąsiednie piksele - liczymy je jako jedną minucję
                bool alreadyFound = minutiae.Any(m =>
                    m.Type == type && Math.Abs(m.X - (x - 1)) <= 2 && Math.Abs(m.Y - (y - 1)) <= 2);
                if (!alreadyFound)
                {
                    // x - 1, y - 1: współrzędne w obrazie bez ramki
                    minutiae.Add(new Minutia(x - 1, y - 1, type.Value, crossingNumber));
                }
            }
        }
        return minutiae;
    }
}
