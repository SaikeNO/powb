namespace Minucje.Core.Preprocessing;

// binaryzacja z progiem lokalnym: piksel jest linią, jeśli jest ciemniejszy niż średnia wokół niego
// (próg lokalny, bo palec jest dociśnięty nierówno i jasność zmienia się na odcisku)
public static class Binarization
{
    private const int SmoothingRadius = 2;   // rozmycie 5x5 - skleja drobne przerwy w liniach
    private const int LocalMeanRadius = 7;   // średnia z okna 15x15 - lokalny próg

    public static byte[] Apply(byte[] grayscale, int width, int height, bool[] fingerprintMask)
    {
        byte[] smoothed = BoxBlur.Apply(grayscale, width, height, SmoothingRadius);
        byte[] localMean = BoxBlur.Apply(smoothed, width, height, LocalMeanRadius);

        // wynik z ramką 1 piksela (patrz Neighbourhood)
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
