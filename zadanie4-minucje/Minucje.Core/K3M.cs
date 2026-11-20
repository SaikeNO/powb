namespace Minucje.Core;

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

    // A1pix: końcowe ścienienie do grubości dokładnie 1 piksela (w zadaniu 3 to ta sama lista co A0)
    private static readonly int[] OnePixelWidthWeights = BorderWeights;

    public static byte[] Thin(byte[] image, int paddedWidth, int paddedHeight)
    {
        var result = (byte[])image.Clone();
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            // faza 0: zapamiętujemy piksele brzegowe
            var borderPixels = new List<int>();
            ForEachPixel(paddedWidth, paddedHeight, index =>
            {
                if (result[index] == 1 && BorderWeights.Contains(Neighbourhood.GetWeight(result, index, paddedWidth)))
                {
                    borderPixels.Add(index);
                }
            });

            // fazy 1-5: usuwamy piksele brzegowe, których otoczenie jest na liście danej fazy
            foreach (int[] phase in PhaseWeights)
            {
                foreach (int index in borderPixels)
                {
                    if (result[index] == 1 && phase.Contains(Neighbourhood.GetWeight(result, index, paddedWidth)))
                    {
                        result[index] = 0;
                        anyPixelDeleted = true;
                    }
                }
            }
        }

        // faza końcowa: usuwa zbędne piksele, żeby linia miała 1 piksel grubości
        ForEachPixel(paddedWidth, paddedHeight, index =>
        {
            if (result[index] == 1 && OnePixelWidthWeights.Contains(Neighbourhood.GetWeight(result, index, paddedWidth)))
            {
                result[index] = 0;
            }
        });

        return result;
    }

    // przechodzi po wszystkich pikselach obrazu (bez ramki)
    private static void ForEachPixel(int paddedWidth, int paddedHeight, Action<int> action)
    {
        for (int y = 1; y < paddedHeight - 1; y++)
        {
            for (int x = 1; x < paddedWidth - 1; x++)
            {
                action(y * paddedWidth + x);
            }
        }
    }
}
