namespace Minucje.Core.Imaging;

// obraz binarny ma ramkę 1 piksela tła, więc każdy piksel obrazu ma 8 sąsiadów
public static class Neighbourhood
{
    // sąsiedzi zgodnie z ruchem wskazówek zegara, od góry:
    //   P8 P1 P2
    //   P7  P P3
    //   P6 P5 P4
    public static int[] GetClockwise(byte[] image, int pixelIndex, int paddedWidth)
    {
        return new int[]
        {
            image[pixelIndex - paddedWidth],
            image[pixelIndex - paddedWidth + 1],
            image[pixelIndex + 1],
            image[pixelIndex + paddedWidth + 1],
            image[pixelIndex + paddedWidth],
            image[pixelIndex + paddedWidth - 1],
            image[pixelIndex - 1],
            image[pixelIndex - paddedWidth - 1],
        };
    }

    // waga dla K3M: każdy sąsiad-linia dodaje swoją potęgę dwójki (P1 = 1, P2 = 2, P3 = 4 ... P8 = 128)
    public static int GetWeight(byte[] image, int pixelIndex, int paddedWidth)
    {
        return image[pixelIndex - paddedWidth]
             | image[pixelIndex - paddedWidth + 1] << 1
             | image[pixelIndex + 1] << 2
             | image[pixelIndex + paddedWidth + 1] << 3
             | image[pixelIndex + paddedWidth] << 4
             | image[pixelIndex + paddedWidth - 1] << 5
             | image[pixelIndex - 1] << 6
             | image[pixelIndex - paddedWidth - 1] << 7;
    }
}
