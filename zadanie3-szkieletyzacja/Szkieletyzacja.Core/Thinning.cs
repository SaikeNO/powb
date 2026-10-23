namespace Szkieletyzacja.Core;

public static class Thinning
{
    private const byte Background = 0;
    private const byte Object = 1;
    private const byte EdgeContour = 2;
    private const byte CornerContour = 3;

    private static readonly bool[] ZhangSuenFirstSubIteration = BuildZhangSuenTable(isFirstSubIteration: true);
    private static readonly bool[] ZhangSuenSecondSubIteration = BuildZhangSuenTable(isFirstSubIteration: false);
    private static readonly bool[] GuoHallFirstSubIteration = BuildGuoHallTable(isFirstSubIteration: true);
    private static readonly bool[] GuoHallSecondSubIteration = BuildGuoHallTable(isFirstSubIteration: false);

    public static byte[] Run(ThinningAlgorithm algorithm, byte[] image, int paddedWidth, int paddedHeight)
    {
        switch (algorithm)
        {
            case ThinningAlgorithm.ZhangSuen:
                return ZhangSuen(image, paddedWidth, paddedHeight);
            case ThinningAlgorithm.GuoHall:
                return GuoHall(image, paddedWidth, paddedHeight);
            case ThinningAlgorithm.Kmm:
                return Kmm(image, paddedWidth, paddedHeight);
            case ThinningAlgorithm.K3M:
                return K3M(image, paddedWidth, paddedHeight);
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm));
        }
    }

    public static byte[] ZhangSuen(byte[] image, int paddedWidth, int paddedHeight)
    {
        return ThinWithTwoSubIterations(
            image, paddedWidth, paddedHeight, ZhangSuenFirstSubIteration, ZhangSuenSecondSubIteration);
    }

    public static byte[] GuoHall(byte[] image, int paddedWidth, int paddedHeight)
    {
        return ThinWithTwoSubIterations(
            image, paddedWidth, paddedHeight, GuoHallFirstSubIteration, GuoHallSecondSubIteration);
    }

    public static byte[] Kmm(byte[] image, int paddedWidth, int paddedHeight)
    {
        var result = (byte[])image.Clone();
        var contourPixels = new int[result.Length];
        var contourLabels = new[] { EdgeContour, CornerContour };
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            int contourCount = LabelContour(result, paddedWidth, paddedHeight, contourPixels);

            int pixelsToDeleteCount = 0;
            for (int contourIndex = 0; contourIndex < contourCount; contourIndex++)
            {
                int pixelIndex = contourPixels[contourIndex];
                int weight = GetNonZeroNeighbourhoodWeight(result, pixelIndex, paddedWidth);
                if (LookupTables.KmmContourWithTwoToFourNeighbours[weight])
                {
                    contourPixels[pixelsToDeleteCount] = pixelIndex;
                    pixelsToDeleteCount++;
                }
            }

            for (int deleteIndex = 0; deleteIndex < pixelsToDeleteCount; deleteIndex++)
            {
                result[contourPixels[deleteIndex]] = Background;
            }
            if (pixelsToDeleteCount > 0)
            {
                anyPixelDeleted = true;
            }

            foreach (byte contourLabel in contourLabels)
            {
                for (int y = 1; y < paddedHeight - 1; y++)
                {
                    int pixelIndex = y * paddedWidth + 1;
                    for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
                    {
                        if (result[pixelIndex] != contourLabel)
                        {
                            continue;
                        }

                        int weight = GetNonZeroNeighbourhoodWeight(result, pixelIndex, paddedWidth);
                        if (LookupTables.KmmDeletionArray[weight])
                        {
                            result[pixelIndex] = Background;
                            anyPixelDeleted = true;
                        }
                        else
                        {
                            result[pixelIndex] = Object;
                        }
                    }
                }
            }
        }

        return result;
    }

    public static byte[] K3M(byte[] image, int paddedWidth, int paddedHeight)
    {
        var result = (byte[])image.Clone();
        var borderPixels = new int[result.Length];
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            int borderCount = 0;
            for (int y = 1; y < paddedHeight - 1; y++)
            {
                int pixelIndex = y * paddedWidth + 1;
                for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
                {
                    if (result[pixelIndex] != Background &&
                        LookupTables.K3MBorderArray[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
                    {
                        borderPixels[borderCount] = pixelIndex;
                        borderCount++;
                    }
                }
            }

            foreach (bool[] deletionPhase in LookupTables.K3MDeletionPhases)
            {
                for (int borderIndex = 0; borderIndex < borderCount; borderIndex++)
                {
                    int pixelIndex = borderPixels[borderIndex];
                    if (result[pixelIndex] != Background &&
                        deletionPhase[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
                    {
                        result[pixelIndex] = Background;
                        anyPixelDeleted = true;
                    }
                }
            }
        }

        for (int y = 1; y < paddedHeight - 1; y++)
        {
            int pixelIndex = y * paddedWidth + 1;
            for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
            {
                if (result[pixelIndex] != Background &&
                    LookupTables.K3MOnePixelWidthArray[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
                {
                    result[pixelIndex] = Background;
                }
            }
        }

        return result;
    }

    private static byte[] ThinWithTwoSubIterations(byte[] image, int paddedWidth, int paddedHeight,
        bool[] firstSubIteration, bool[] secondSubIteration)
    {
        var result = (byte[])image.Clone();
        var pixelsToDelete = new int[result.Length];
        var subIterations = new[] { firstSubIteration, secondSubIteration };
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            foreach (bool[] deletionTable in subIterations)
            {
                int pixelsToDeleteCount = 0;
                for (int y = 1; y < paddedHeight - 1; y++)
                {
                    int pixelIndex = y * paddedWidth + 1;
                    for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
                    {
                        if (result[pixelIndex] != Background &&
                            deletionTable[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
                        {
                            pixelsToDelete[pixelsToDeleteCount] = pixelIndex;
                            pixelsToDeleteCount++;
                        }
                    }
                }

                for (int deleteIndex = 0; deleteIndex < pixelsToDeleteCount; deleteIndex++)
                {
                    result[pixelsToDelete[deleteIndex]] = Background;
                }

                if (pixelsToDeleteCount > 0)
                {
                    anyPixelDeleted = true;
                }
            }
        }

        return result;
    }

    private static int LabelContour(byte[] image, int paddedWidth, int paddedHeight, int[] contourPixels)
    {
        int contourCount = 0;
        for (int y = 1; y < paddedHeight - 1; y++)
        {
            int pixelIndex = y * paddedWidth + 1;
            for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
            {
                if (image[pixelIndex] == Background)
                {
                    continue;
                }

                bool touchesBackgroundByEdge =
                    image[pixelIndex - paddedWidth] == Background || image[pixelIndex + 1] == Background ||
                    image[pixelIndex + paddedWidth] == Background || image[pixelIndex - 1] == Background;
                bool touchesBackgroundByCorner =
                    image[pixelIndex - paddedWidth + 1] == Background || image[pixelIndex + paddedWidth + 1] == Background ||
                    image[pixelIndex + paddedWidth - 1] == Background || image[pixelIndex - paddedWidth - 1] == Background;

                if (touchesBackgroundByEdge)
                {
                    image[pixelIndex] = EdgeContour;
                }
                else if (touchesBackgroundByCorner)
                {
                    image[pixelIndex] = CornerContour;
                }
                else
                {
                    continue;
                }

                contourPixels[contourCount] = pixelIndex;
                contourCount++;
            }
        }
        return contourCount;
    }

    private static int GetNeighbourhoodWeight(byte[] image, int pixelIndex, int paddedWidth)
    {
        return image[pixelIndex - paddedWidth]  // N  = 1
            + (image[pixelIndex - paddedWidth + 1] << 1)  // NE = 2
            + (image[pixelIndex + 1] << 2)  // E  = 4
            + (image[pixelIndex + paddedWidth + 1] << 3)  // SE = 8
            + (image[pixelIndex + paddedWidth] << 4)  // S  = 16
            + (image[pixelIndex + paddedWidth - 1] << 5)  // SW = 32
            + (image[pixelIndex - 1] << 6)  // W  = 64
            + (image[pixelIndex - paddedWidth - 1] << 7);  // NW = 128
    }

    private static int GetNonZeroNeighbourhoodWeight(byte[] image, int pixelIndex, int paddedWidth)
    {
        return ToBit(image[pixelIndex - paddedWidth])
            + (ToBit(image[pixelIndex - paddedWidth + 1]) << 1)
            + (ToBit(image[pixelIndex + 1]) << 2)
            + (ToBit(image[pixelIndex + paddedWidth + 1]) << 3)
            + (ToBit(image[pixelIndex + paddedWidth]) << 4)
            + (ToBit(image[pixelIndex + paddedWidth - 1]) << 5)
            + (ToBit(image[pixelIndex - 1]) << 6)
            + (ToBit(image[pixelIndex - paddedWidth - 1]) << 7);
    }

    private static int ToBit(byte label)
    {
        return label + 3 >> 2;
    }

    private static bool[] BuildZhangSuenTable(bool isFirstSubIteration)
    {
        // tablica decyzji: indeks = waga otoczenia (0-255), wartoœæ = czy usun¹æ piksel
        var table = new bool[256];

        // sprawdzamy po kolei ka¿de mo¿liwe otoczenie piksela
        for (int weight = 0; weight < 256; weight++)
        {
            // wyci¹gamy z wagi czterech s¹siadów (1 = obiekt, 0 = t³o)
            int north = weight & 1; // czy N = 1
            int east = weight >> 2 & 1; // czy E = 1
            int south = weight >> 4 & 1; // czy S = 1
            int west = weight >> 6 & 1; // czy W = 1

            // ilu s¹siadów (ze wszystkich oœmiu) jest obiektem
            int neighbourCount = CountNeighbours(weight);

            // jeœli krawêdŸ, œrodek lub rozerwanie (>1 0->1) nie usuwamy
            // < 2 : koniec linii, > 6 : wnêtrze, != 1 : s¹siedzi tworz¹ wiêcej ni¿ jedn¹ grupê
            if (neighbourCount < 2 || neighbourCount > 6 || CountBackgroundToObjectTransitions(weight) != 1)
            {
                continue; // zostaje false
            }

            // warunek kierunkowy (iloczyn = 1 tylko gdy wszyscy trzej s¹siedzi s¹ obiektem)
            if (isFirstSubIteration)
            {
                // przejœcie 1: nie usuwamy, gdy N,E,S albo E,S,W s¹ jednoczeœnie obiektem (usuwa doln¹ i praw¹ stronê)
                table[weight] = north * east * south == 0 && east * south * west == 0;
            }
            else
            {
                // przejœcie 2: nie usuwamy, gdy N,E,W albo N,S,W s¹ jednoczeœnie obiektem (usuwa górn¹ i lew¹ stronê)
                table[weight] = north * east * west == 0 && north * south * west == 0;
            }
        }
        return table;
    }

    private static bool[] BuildGuoHallTable(bool isFirstSubIteration)
    {
        var table = new bool[256];
        for (int weight = 0; weight < 256; weight++)
        {
            bool north = IsBitSet(weight, 0);
            bool northEast = IsBitSet(weight, 1);
            bool east = IsBitSet(weight, 2);
            bool southEast = IsBitSet(weight, 3);
            bool south = IsBitSet(weight, 4);
            bool southWest = IsBitSet(weight, 5);
            bool west = IsBitSet(weight, 6);
            bool northWest = IsBitSet(weight, 7);

            int connectivity =
                (!north && (northEast || east) ? 1 : 0) + (!east && (southEast || south) ? 1 : 0) +
                (!south && (southWest || west) ? 1 : 0) + (!west && (northWest || north) ? 1 : 0);
            int pairsStartingAtNorthWest =
                ((northWest || north) ? 1 : 0) + ((northEast || east) ? 1 : 0) +
                ((southEast || south) ? 1 : 0) + ((southWest || west) ? 1 : 0);
            int pairsStartingAtNorth =
                ((north || northEast) ? 1 : 0) + ((east || southEast) ? 1 : 0) +
                ((south || southWest) ? 1 : 0) + ((west || northWest) ? 1 : 0);
            int minimumPairs = Math.Min(pairsStartingAtNorthWest, pairsStartingAtNorth);

            if (connectivity != 1 || minimumPairs < 2 || minimumPairs > 3)
            {
                continue;
            }

            if (isFirstSubIteration)
            {
                table[weight] = !((north || northEast || !southEast) && east);
            }
            else
            {
                table[weight] = !((south || southWest || !northWest) && west);
            }
        }
        return table;
    }

    private static bool IsBitSet(int value, int bitIndex)
    {
        return (value >> bitIndex & 1) == 1;
    }

    private static int CountNeighbours(int weight)
    {
        int count = 0;
        for (int bitIndex = 0; bitIndex < 8; bitIndex++)
        {
            if (IsBitSet(weight, bitIndex))
            {
                count++;
            }
        }
        return count;
    }

    private static int CountBackgroundToObjectTransitions(int weight)
    {
        int transitions = 0;
        for (int bitIndex = 0; bitIndex < 8; bitIndex++)
        {
            // zliczanie przejœæ 0 -> 1
            if (!IsBitSet(weight, bitIndex) && IsBitSet(weight, (bitIndex + 1) & 7))
            {
                transitions++;
            }
        }
        return transitions;
    }
}
