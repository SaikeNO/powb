namespace Minucje.Core.Imaging;

public static class Neighbourhood
{
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
