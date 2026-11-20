namespace Minucje.Core;

// Obrazy binarne w projekcie to tablice byte[] (0 = tło, 1 = linia) z ramką 1 piksela tła dookoła,
// tak jak w zadaniu 3. Dzięki ramce każdy piksel obrazu ma 8 sąsiadów i nie trzeba sprawdzać granic.
// Piksel (x, y) obrazu leży pod indeksem (y + 1) * paddedWidth + (x + 1).
//
// Sąsiedzi piksela P, zgodnie z ruchem wskazówek zegara, zaczynając od góry:
//
//   P8  P1  P2        NW  N  NE
//   P7  P   P3        W   P  E
//   P6  P5  P4        SW  S  SE
public static class Neighbourhood
{
    // wartości P1..P8 (0 lub 1) w kolejności jak na rysunku
    public static int[] GetClockwise(byte[] image, int index, int paddedWidth)
    {
        return new int[]
        {
            image[index - paddedWidth],      // P1 = N
            image[index - paddedWidth + 1],  // P2 = NE
            image[index + 1],                // P3 = E
            image[index + paddedWidth + 1],  // P4 = SE
            image[index + paddedWidth],      // P5 = S
            image[index + paddedWidth - 1],  // P6 = SW
            image[index - 1],                // P7 = W
            image[index - paddedWidth - 1],  // P8 = NW
        };
    }

    // waga otoczenia dla K3M: sąsiad Pi będący linią dodaje 2^(i-1)
    //   128  1   2
    //    64  P   4
    //    32  16  8
    public static int GetWeight(byte[] image, int index, int paddedWidth)
    {
        int[] neighbours = GetClockwise(image, index, paddedWidth);
        int weight = 0;
        for (int i = 0; i < 8; i++)
        {
            weight += neighbours[i] << i;
        }
        return weight;
    }
}
