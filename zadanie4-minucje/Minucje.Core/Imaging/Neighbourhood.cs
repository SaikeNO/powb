namespace Minucje.Core.Imaging;

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
    public static int[] GetClockwise(byte[] image, int pixelIndex, int paddedWidth)
    {
        return new int[]
        {
            image[pixelIndex - paddedWidth],      // P1 = N
            image[pixelIndex - paddedWidth + 1],  // P2 = NE
            image[pixelIndex + 1],                // P3 = E
            image[pixelIndex + paddedWidth + 1],  // P4 = SE
            image[pixelIndex + paddedWidth],      // P5 = S
            image[pixelIndex + paddedWidth - 1],  // P6 = SW
            image[pixelIndex - 1],                // P7 = W
            image[pixelIndex - paddedWidth - 1],  // P8 = NW
        };
    }

    // Waga otoczenia dla K3M: sąsiad Pi będący linią dodaje 2^(i-1).
    //   128  1   2
    //    64  P   4
    //    32  16  8
    // Liczona bez tworzenia tablicy, bo K3M woła ją setki tysięcy razy.
    public static int GetWeight(byte[] image, int pixelIndex, int paddedWidth)
    {
        return image[pixelIndex - paddedWidth]               // P1 = N  -> 1
             | image[pixelIndex - paddedWidth + 1] << 1      // P2 = NE -> 2
             | image[pixelIndex + 1] << 2                    // P3 = E  -> 4
             | image[pixelIndex + paddedWidth + 1] << 3      // P4 = SE -> 8
             | image[pixelIndex + paddedWidth] << 4          // P5 = S  -> 16
             | image[pixelIndex + paddedWidth - 1] << 5      // P6 = SW -> 32
             | image[pixelIndex - 1] << 6                    // P7 = W  -> 64
             | image[pixelIndex - paddedWidth - 1] << 7;     // P8 = NW -> 128
    }
}
