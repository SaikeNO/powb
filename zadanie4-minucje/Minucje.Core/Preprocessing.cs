namespace Minucje.Core;

// Przygotowanie odcisku przed szkieletyzacją:
//  1. maska - gdzie na obrazie jest odcisk, a gdzie puste tło skanera,
//  2. binaryzacja - które piksele to linie papilarne (1), a które doliny między nimi (0).
// Obraz wejściowy: jasność 0-255, width × height, linie papilarne są ciemne.
public static class Preprocessing
{
    private const int BlockSize = 8;
    private const double MinContrast = 0.3;
    private const int BlurRadius = 2;       // rozmycie 5×5
    private const int LocalMeanRadius = 7;  // średnia z okna 15×15

    // Maska odcisku. Dzielimy obraz na bloki 8×8 i liczymy w każdym odchylenie standardowe jasności.
    // Na odcisku linie i doliny się przeplatają, więc jasność mocno się zmienia, a tło skanera jest jednolite.
    // Blok należy do odcisku, jeśli jego odchylenie jest większe niż 30% największego w obrazie.
    public static bool[] ComputeMask(byte[] grayscale, int width, int height)
    {
        int blocksX = (width + BlockSize - 1) / BlockSize;
        int blocksY = (height + BlockSize - 1) / BlockSize;
        var stdDev = new double[blocksX, blocksY];

        for (int bx = 0; bx < blocksX; bx++)
        {
            for (int by = 0; by < blocksY; by++)
            {
                stdDev[bx, by] = BlockStdDev(grayscale, width, height, bx * BlockSize, by * BlockSize);
            }
        }

        double maxStdDev = stdDev.Cast<double>().Max();
        var hasRidges = new bool[blocksX, blocksY];
        for (int bx = 0; bx < blocksX; bx++)
        {
            for (int by = 0; by < blocksY; by++)
            {
                hasRidges[bx, by] = stdDev[bx, by] > MinContrast * maxStdDev;
            }
        }

        // Odcisk nie ma dziur: blok należy do maski, jeśli bloki z liniami są i po jego lewej i prawej stronie
        // (w tym samym wierszu), i nad nim i pod nim (w tej samej kolumnie). Wypełnia to bloki o słabym kontraście
        // w środku odcisku, a pomija pojedyncze zabrudzenia w tle.
        var mask = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int bx = x / BlockSize;
                int by = y / BlockSize;
                bool insideRow = AnyInRow(hasRidges, by, 0, bx) && AnyInRow(hasRidges, by, bx, blocksX - 1);
                bool insideColumn = AnyInColumn(hasRidges, bx, 0, by) && AnyInColumn(hasRidges, bx, by, blocksY - 1);
                mask[y * width + x] = insideRow && insideColumn;
            }
        }
        return mask;
    }

    // Binaryzacja z progiem lokalnym: piksel jest linią, jeśli jest ciemniejszy niż średnia jasność wokół niego.
    // Jeden próg dla całego obrazu (np. Otsu) nie działa, bo palec jest dociśnięty nierówno: część odcisku jest
    // ciemna, część jasna. Rozmycie 5×5 wcześniej skleja drobne przerwy w liniach (pory skóry, suchy palec).
    // Wynik: obraz 0/1 z ramką 1 piksela (patrz Neighbourhood), poza maską same zera.
    public static byte[] Binarize(byte[] grayscale, int width, int height, bool[] mask)
    {
        byte[] smoothed = BoxBlur(grayscale, width, height, BlurRadius);
        byte[] localMean = BoxBlur(smoothed, width, height, LocalMeanRadius);

        int paddedWidth = width + 2;
        var binary = new byte[paddedWidth * (height + 2)];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                if (mask[i] && smoothed[i] < localMean[i])
                {
                    binary[(y + 1) * paddedWidth + (x + 1)] = 1;
                }
            }
        }
        return binary;
    }

    // Średnia jasność w kwadracie (2·radius+1)×(2·radius+1) wokół każdego piksela.
    // Liczona osobno w poziomie i w pionie - wynik jest ten sam, a liczenie dużo szybsze.
    public static byte[] BoxBlur(byte[] image, int width, int height, int radius)
    {
        var horizontal = new byte[image.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                int count = 0;
                for (int nx = Math.Max(0, x - radius); nx <= Math.Min(width - 1, x + radius); nx++)
                {
                    sum += image[y * width + nx];
                    count++;
                }
                horizontal[y * width + x] = (byte)(sum / count);
            }
        }

        var result = new byte[image.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                int count = 0;
                for (int ny = Math.Max(0, y - radius); ny <= Math.Min(height - 1, y + radius); ny++)
                {
                    sum += horizontal[ny * width + x];
                    count++;
                }
                result[y * width + x] = (byte)(sum / count);
            }
        }
        return result;
    }

    private static double BlockStdDev(byte[] grayscale, int width, int height, int left, int top)
    {
        var values = new List<double>();
        for (int y = top; y < Math.Min(height, top + BlockSize); y++)
        {
            for (int x = left; x < Math.Min(width, left + BlockSize); x++)
            {
                values.Add(grayscale[y * width + x]);
            }
        }

        double mean = values.Average();
        double variance = values.Average(value => (value - mean) * (value - mean));
        return Math.Sqrt(variance);
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
