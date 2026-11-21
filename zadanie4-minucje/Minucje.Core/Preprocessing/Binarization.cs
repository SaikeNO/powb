namespace Minucje.Core.Preprocessing;

public static class Binarization
{
    private const int SmoothingRadius = 2;
    private const int LocalMeanRadius = 7;

    public static byte[] Apply(byte[] grayscale, int width, int height, bool[] fingerprintMask)
    {
        byte[] smoothed = BoxBlur.Apply(grayscale, width, height, SmoothingRadius);
        byte[] localMean = BoxBlur.Apply(smoothed, width, height, LocalMeanRadius);

        int paddedWidth = width + 2;
        var binaryImage = new byte[paddedWidth * (height + 2)];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = y * width + x;
                bool isRidge = fingerprintMask[pixelIndex] && smoothed[pixelIndex] < localMean[pixelIndex];
                if (isRidge)
                {
                    binaryImage[(y + 1) * paddedWidth + (x + 1)] = 1;
                }
            }
        }
        return binaryImage;
    }
}
