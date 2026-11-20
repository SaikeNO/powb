namespace Minucje.Core.Preprocessing;

// Binaryzacja z progiem lokalnym: piksel jest linią, jeśli jest ciemniejszy niż średnia jasność wokół niego.
// Jeden próg dla całego obrazu (np. Otsu) nie działa, bo palec jest dociśnięty nierówno: część odcisku jest
// ciemna, część jasna. Rozmycie 5×5 wcześniej skleja drobne przerwy w liniach (pory skóry, suchy palec).
// Wynik: obraz 0/1 z ramką 1 piksela (patrz Neighbourhood), poza maską same zera.
public static class Binarization
{
    private const int SmoothingRadius = 2;   // rozmycie 5×5
    private const int LocalMeanRadius = 7;   // średnia z okna 15×15

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
