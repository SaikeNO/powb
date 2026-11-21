namespace Minucje.Core.Preprocessing;

// maska odcisku: true = odcisk, false = tło skanera
public static class FingerprintMask
{
    private const int BlockSize = 8;
    private const double MinContrastRatio = 0.3;

    public static bool[] Compute(byte[] grayscale, int width, int height)
    {
        int blockColumns = (width + BlockSize - 1) / BlockSize;
        int blockRows = (height + BlockSize - 1) / BlockSize;

        // odchylenie standardowe jasności w blokach 8x8: na odcisku duże (linie i doliny), na tle małe
        var blockStdDev = new double[blockColumns, blockRows];
        for (int column = 0; column < blockColumns; column++)
        {
            for (int row = 0; row < blockRows; row++)
            {
                blockStdDev[column, row] = BlockStdDev(grayscale, width, height, column * BlockSize, row * BlockSize);
            }
        }

        // blok ma linie, jeśli jego odchylenie > 30% największego
        double maxStdDev = blockStdDev.Cast<double>().Max();
        var blockHasRidges = new bool[blockColumns, blockRows];
        for (int column = 0; column < blockColumns; column++)
        {
            for (int row = 0; row < blockRows; row++)
            {
                blockHasRidges[column, row] = blockStdDev[column, row] > MinContrastRatio * maxStdDev;
            }
        }

        // wypełnianie dziur: blok należy do odcisku, jeśli ma bloki z liniami z obu stron w poziomie i w pionie
        var blockInMask = new bool[blockColumns, blockRows];
        for (int column = 0; column < blockColumns; column++)
        {
            for (int row = 0; row < blockRows; row++)
            {
                bool ridgesLeftAndRight = AnyInRow(blockHasRidges, row, 0, column)
                                          && AnyInRow(blockHasRidges, row, column, blockColumns - 1);
                bool ridgesAboveAndBelow = AnyInColumn(blockHasRidges, column, 0, row)
                                           && AnyInColumn(blockHasRidges, column, row, blockRows - 1);
                blockInMask[column, row] = ridgesLeftAndRight && ridgesAboveAndBelow;
            }
        }

        // maska bloków -> maska pikseli
        var mask = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mask[y * width + x] = blockInMask[x / BlockSize, y / BlockSize];
            }
        }
        return mask;
    }

    private static double BlockStdDev(byte[] grayscale, int width, int height, int left, int top)
    {
        int right = Math.Min(width, left + BlockSize);
        int bottom = Math.Min(height, top + BlockSize);
        int pixelCount = (right - left) * (bottom - top);

        double sum = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                sum += grayscale[y * width + x];
            }
        }
        double mean = sum / pixelCount;

        double squaredDifferences = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                double difference = grayscale[y * width + x] - mean;
                squaredDifferences += difference * difference;
            }
        }
        return Math.Sqrt(squaredDifferences / pixelCount);
    }

    private static bool AnyInRow(bool[,] blocks, int row, int fromColumn, int toColumn)
    {
        for (int column = fromColumn; column <= toColumn; column++)
        {
            if (blocks[column, row])
            {
                return true;
            }
        }
        return false;
    }

    private static bool AnyInColumn(bool[,] blocks, int column, int fromRow, int toRow)
    {
        for (int row = fromRow; row <= toRow; row++)
        {
            if (blocks[column, row])
            {
                return true;
            }
        }
        return false;
    }
}
