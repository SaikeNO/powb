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
        return ThinWithTwoSubIterations(image, paddedWidth, paddedHeight, ZhangSuenFirstSubIteration, ZhangSuenSecondSubIteration);
    }

    public static byte[] GuoHall(byte[] image, int paddedWidth, int paddedHeight)
    {
        return ThinWithTwoSubIterations(image, paddedWidth, paddedHeight, GuoHallFirstSubIteration, GuoHallSecondSubIteration);
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

            // liczba pikseli konturu i piksele konturu + przypisanie labelek: 2 - edge, 3 - corner
            int contourCount = LabelContour(result, paddedWidth, paddedHeight, contourPixels);

            // z konturu wybieramy naro¿niki i przepisujemy je na pocz¹tek listy - do usuniêcia
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

            // najpierw edge = 2, potem corner = 3
            foreach (byte contourLabel in contourLabels)
            {
                for (int y = 1; y < paddedHeight - 1; y++)
                {
                    int pixelIndex = y * paddedWidth + 1;
                    for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
                    {
                        // pomijamy t³o, wnêtrze i piksele o innej etykiecie ni¿ aktualnie przetwarzana
                        if (result[pixelIndex] != contourLabel)
                        {
                            continue;
                        }

                        // liczymy wagê i usuwamy jeœli jest na liœcie, jeœli nie ma to dajemy label object = 1
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

            // lista pikseli brzegowych
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

            // przechodzimy 5 razy po tej samej liœcie brzegu za ka¿dym razem z inn¹ tablic¹ (A1, A2, ... A5)
            // zaczynamy od najbezpieczniejszych usuniêæ potem coraz analizujemy coraz wiêksz¹ liczbê s¹siadów (3, 3-4, 3–5, 3–6, 3-7)
            foreach (bool[] deletionPhase in LookupTables.K3MDeletionPhases)
            {
                for (int borderIndex = 0; borderIndex < borderCount; borderIndex++)
                {
                    int pixelIndex = borderPixels[borderIndex];

                    // jeœli na aktualnej liœcie do usuniêcia oraz nie jest t³em to usuwamy
                    if (result[pixelIndex] != Background && deletionPhase[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
                    {
                        result[pixelIndex] = Background;
                        anyPixelDeleted = true;
                    }
                }
            }
        }

        // faza koñcowa (A1pix): usuwa nadmiarowe piksele (kolce, podwójne linie)
        for (int y = 1; y < paddedHeight - 1; y++)
        {
            int pixelIndex = y * paddedWidth + 1;
            for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
            {
                if (result[pixelIndex] != Background && LookupTables.K3MOnePixelWidthArray[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
                {
                    result[pixelIndex] = Background;
                }
            }
        }

        return result;
    }

    private static byte[] ThinWithTwoSubIterations(byte[] image, int paddedWidth, int paddedHeight, bool[] firstSubIteration, bool[] secondSubIteration)
    {
        var result = (byte[])image.Clone();
        var pixelsToDelete = new int[result.Length];
        var subIterations = new[] { firstSubIteration, secondSubIteration };
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            // 1 iteracja - usuwanie dolnej i prawej strony
            // 2 iteracja - usuwanie górnej i lewej strony
            foreach (bool[] deletionTable in subIterations)
            {
                int pixelsToDeleteCount = 0;
                for (int y = 1; y < paddedHeight - 1; y++)
                {
                    int pixelIndex = y * paddedWidth + 1;
                    for (int x = 1; x < paddedWidth - 1; x++, pixelIndex++)
                    {
                        // jeœli nie t³o i przy takim ustawieniu s¹siadów powinniœmy usun¹æ to usuwamy
                        if (result[pixelIndex] != Background && deletionTable[GetNeighbourhoodWeight(result, pixelIndex, paddedWidth)])
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

                // czy piksel styka siê z t³em bokiem
                bool touchesBackgroundByEdge =
                    image[pixelIndex - paddedWidth] == Background || image[pixelIndex + 1] == Background ||
                    image[pixelIndex + paddedWidth] == Background || image[pixelIndex - 1] == Background;

                // czy piksel styka siê z t³em rogiem
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

    // to samo co GetNeighbourhoodWeight, ale obraz ma ju¿ etykiety 0-4, wiêc ka¿dy niezerowy piksel liczy siê jako obiekt
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
        return label != 0 ? 1 : 0;
    }

    private static bool[] BuildZhangSuenTable(bool isFirstSubIteration)
    {
        var table = new bool[256];

        for (int weight = 0; weight < 256; weight++)
        {
            // sprawdzamy czy s¹siedzi to obiekt
            bool north = IsBitSet(weight, 0);
            bool east = IsBitSet(weight, 2);
            bool south = IsBitSet(weight, 4);
            bool west = IsBitSet(weight, 6);

            int neighbourCount = CountNeighbours(weight);

            // jeœli koniec linii, œrodek lub rozerwanie (>1 0->1) nie usuwamy
            if (neighbourCount < 2 || neighbourCount > 6 || CountBackgroundToObjectTransitions(weight) != 1)
            {
                continue;
            }

            if (isFirstSubIteration)
            {
                // usuwa doln¹ i praw¹ stronê
                table[weight] = !(north && east && south) && !(east && south && west);
            }
            else
            {
                // usuwa górn¹ i lew¹ stronê
                table[weight] = !(north && east && west) && !(north && south && west);
            }
        }
        return table;
    }

    private static bool[] BuildGuoHallTable(bool isFirstSubIteration)
    {
        var table = new bool[256];

        for (int weight = 0; weight < 256; weight++)
        {
            // sprawdzamy czy s¹siedzi to obiekt
            bool north = IsBitSet(weight, 0);
            bool northEast = IsBitSet(weight, 1);
            bool east = IsBitSet(weight, 2);
            bool southEast = IsBitSet(weight, 3);
            bool south = IsBitSet(weight, 4);
            bool southWest = IsBitSet(weight, 5);
            bool west = IsBitSet(weight, 6);
            bool northWest = IsBitSet(weight, 7);

            // ile jest osobnych grup s¹siadów
            int connectivity =
                ToInt(!north && (northEast || east)) +
                ToInt(!east && (southEast || south)) +
                ToInt(!south && (southWest || west)) +
                ToInt(!west && (northWest || north));

            // s¹siadów ³¹czymy w 4 pary liczymy pary, w których jest choæ jeden obiekt
            // pary mo¿na u³o¿yæ na dwa sposoby
            int pairsStartingAtNorthWest =
                ToInt(northWest || north) +
                ToInt(northEast || east) +
                ToInt(southEast || south) +
                ToInt(southWest || west);
            int pairsStartingAtNorth =
                ToInt(north || northEast) +
                ToInt(east || southEast) +
                ToInt(south || southWest) +
                ToInt(west || northWest);

            // bierzemy mniejsz¹ z dwóch liczb: 0-1 = koniec linii, 4 = wnêtrze, 2-3 = brzeg
            int minimumPairs = Math.Min(pairsStartingAtNorthWest, pairsStartingAtNorth);

            // jeœli rozerwanie, koniec linii lub œrodek nie usuwamy
            if (connectivity != 1 || minimumPairs < 2 || minimumPairs > 3)
            {
                continue;
            }

            if (isFirstSubIteration)
            {
                // usuniêcie górnej i prawej strony
                // usuwamy, gdy nie ma s¹siada E albo gdy nad pikselem jest pusto (N, NE puste), a SE jest obiektem
                table[weight] = !east || (!north && !northEast && southEast);
            }
            else
            {
                // usuniêcie dolnej i lewej strony
                // usuwamy, gdy nie ma s¹siada W albo gdy pod pikselem jest pusto (S, SW puste), a NW jest obiektem
                table[weight] = !west || (!south && !southWest && northWest);
            }
        }
        return table;
    }

    private static int ToInt(bool condition)
    {
        return condition ? 1 : 0;
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
