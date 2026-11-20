using Minucje.Core.Imaging;

namespace Minucje.Core.Thinning;

// Szkieletyzacja K3M (Saeed, Tabędzki, Rybnik, Adamski 2010), ta sama co w zadaniu 3.
// Zamienia linie papilarne grubości kilku pikseli na linie grubości 1 piksela,
// bo Crossing Number działa tylko na takim szkielecie.
public static class K3M
{
    // A0: piksele brzegowe (stykają się z tłem)
    private static readonly int[] BorderWeights =
    {
        3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56, 60, 62, 63, 96, 112, 120, 124, 126, 127, 129, 131, 135, 143, 159,
        191, 192, 193, 195, 199, 207, 223, 224, 225, 227, 231, 239, 240, 241, 243, 247, 248, 249, 251, 252, 253, 254,
    };

    // A1..A5: piksele brzegowe, które wolno usunąć w fazach 1..5 (3, 3-4, 3-5, 3-6, 3-7 sąsiadów obok siebie)
    private static readonly int[][] PhaseWeights =
    {
        new[] { 7, 14, 28, 56, 112, 131, 193, 224 },
        new[] { 7, 14, 15, 28, 30, 56, 60, 112, 120, 131, 135, 193, 195, 224, 225, 240 },
        new[]
        {
            7, 14, 15, 28, 30, 31, 56, 60, 62, 112, 120, 124, 131, 135, 143, 193, 195, 199, 224, 225, 227, 240, 241, 248,
        },
        new[]
        {
            7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 193, 195, 199, 207, 224, 225,
            227, 231, 240, 241, 243, 248, 249, 252,
        },
        new[]
        {
            7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 191, 193, 195, 199, 207, 224,
            225, 227, 231, 239, 240, 241, 243, 248, 249, 251, 252, 254,
        },
    };

    // Szybkie sprawdzanie: zamiast szukać wagi na liście (Contains), sprawdzamy tablicę 256 wartości true/false.
    // isBorder[waga] == true  <=>  waga jest na liście BorderWeights
    private static readonly bool[] IsBorder = ToLookup(BorderWeights);
    private static readonly bool[][] CanDeleteInPhase = PhaseWeights.Select(ToLookup).ToArray();

    // A1pix: końcowe ścienienie do grubości dokładnie 1 piksela (w zadaniu 3 to ta sama lista co A0)
    private static readonly bool[] CanDeleteInFinalPass = IsBorder;

    public static byte[] Thin(byte[] binaryImage, int paddedWidth, int paddedHeight)
    {
        var skeleton = (byte[])binaryImage.Clone();
        var borderPixels = new List<int>();
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            // faza 0: zapamiętujemy piksele brzegowe
            borderPixels.Clear();
            for (int y = 1; y < paddedHeight - 1; y++)
            {
                for (int x = 1; x < paddedWidth - 1; x++)
                {
                    int pixelIndex = y * paddedWidth + x;
                    if (skeleton[pixelIndex] == 1 && IsBorder[Neighbourhood.GetWeight(skeleton, pixelIndex, paddedWidth)])
                    {
                        borderPixels.Add(pixelIndex);
                    }
                }
            }

            // fazy 1-5: usuwamy piksele brzegowe, których otoczenie jest na liście danej fazy
            foreach (bool[] canDelete in CanDeleteInPhase)
            {
                foreach (int pixelIndex in borderPixels)
                {
                    if (skeleton[pixelIndex] == 1 && canDelete[Neighbourhood.GetWeight(skeleton, pixelIndex, paddedWidth)])
                    {
                        skeleton[pixelIndex] = 0;
                        anyPixelDeleted = true;
                    }
                }
            }
        }

        // faza końcowa: usuwa zbędne piksele, żeby linia miała 1 piksel grubości
        for (int y = 1; y < paddedHeight - 1; y++)
        {
            for (int x = 1; x < paddedWidth - 1; x++)
            {
                int pixelIndex = y * paddedWidth + x;
                if (skeleton[pixelIndex] == 1 && CanDeleteInFinalPass[Neighbourhood.GetWeight(skeleton, pixelIndex, paddedWidth)])
                {
                    skeleton[pixelIndex] = 0;
                }
            }
        }

        return skeleton;
    }

    private static bool[] ToLookup(int[] weights)
    {
        var lookup = new bool[256];
        foreach (int weight in weights)
        {
            lookup[weight] = true;
        }
        return lookup;
    }
}
