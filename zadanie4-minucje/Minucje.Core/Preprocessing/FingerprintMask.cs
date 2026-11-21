namespace Minucje.Core.Preprocessing;

// MASKA ODCISKU - odpowiada na pytanie: który piksel to palec, a który to puste tło skanera?
//
// Pomysł: tniemy zdjęcie na kwadraciki 8x8 (jak kratka w zeszycie) i w każdym sprawdzamy:
//   - są paski (ciemno-jasno-ciemno)  -> to palec
//   - jest gładko (wszystko podobne)  -> to tło skanera
//
// Wynik: tablica true/false dla każdego piksela (true = palec, false = tło).
public static class FingerprintMask
{
    private const int BlockSize = 8;               // kwadracik ma 8x8 pikseli
    private const double MinContrastRatio = 0.3;   // próg: 30% "pasiastości" najbardziej pasiastego kwadracika

    public static bool[] Compute(byte[] grayscale, int width, int height)
    {
        // ---------- KROK 1: ile kwadracików mieści się na zdjęciu? ----------
        // np. zdjęcie 248 x 338 -> 31 kwadracików w poziomie, 43 w pionie
        // (+ BlockSize - 1 zaokrągla w górę, żeby ostatni, węższy kwadracik też się liczył)
        int blockColumns = (width + BlockSize - 1) / BlockSize;
        int blockRows = (height + BlockSize - 1) / BlockSize;

        // ---------- KROK 2: jak bardzo "pasiasty" jest każdy kwadracik? ----------
        // Dla każdego kwadracika liczymy jedną liczbę - odchylenie standardowe jasności:
        //   tło:   220 220 221 220  -> liczby prawie równe  -> wynik mały (~1)
        //   palec:  40 200  40 200  -> liczby skaczą        -> wynik duży (~80)
        var blockStdDev = new double[blockColumns, blockRows];
        for (int column = 0; column < blockColumns; column++)
        {
            for (int row = 0; row < blockRows; row++)
            {
                // lewy górny róg kwadracika to (column * 8, row * 8)
                blockStdDev[column, row] = BlockStdDev(grayscale, width, height, column * BlockSize, row * BlockSize);
            }
        }

        // ---------- KROK 3: decyzja - palec czy tło? ----------
        // Szukamy najbardziej pasiastego kwadracika na całym zdjęciu (np. 80).
        // Próg = 30% z tego (np. 0.3 * 80 = 24).
        // Kwadracik powyżej progu -> palec (true), poniżej -> tło (false).
        // Próg jest względny (a nie stała liczba), więc działa i dla jasnych, i dla ciemnych zdjęć.
        double maxStdDev = blockStdDev.Cast<double>().Max();
        var blockHasRidges = new bool[blockColumns, blockRows];
        for (int column = 0; column < blockColumns; column++)
        {
            for (int row = 0; row < blockRows; row++)
            {
                blockHasRidges[column, row] = blockStdDev[column, row] > MinContrastRatio * maxStdDev;
            }
        }

        // ---------- KROK 4: łatanie dziur ----------
        // Czasem kwadracik w środku palca wychodzi "gładki" (brud, rozmazanie) - a palec nie ma dziur.
        // Zasada: kwadracik jest palcem, jeśli ma palec i z lewej, i z prawej, i z góry, i z dołu.
        //
        //    .  ✓  ✓  ✓  .         .  ✓  ✓  ✓  .
        //    .  ✓  ✗  ✓  .    ->   .  ✓  ✓  ✓  .     ✗ jest otoczony przez ✓, więc staje się ✓
        //    .  ✓  ✓  ✓  .         .  ✓  ✓  ✓  .
        //
        // Kwadracik w tle nie ma palca po jednej ze stron (np. z prawej jest już tylko tło), więc zostaje tłem.
        var blockInMask = new bool[blockColumns, blockRows];
        for (int column = 0; column < blockColumns; column++)
        {
            for (int row = 0; row < blockRows; row++)
            {
                // w tym samym wierszu: czy jest palec od lewej krawędzi do mnie ORAZ ode mnie do prawej krawędzi?
                bool ridgesLeftAndRight = AnyInRow(blockHasRidges, row, 0, column)
                                          && AnyInRow(blockHasRidges, row, column, blockColumns - 1);
                // w tej samej kolumnie: czy jest palec od góry do mnie ORAZ ode mnie do dołu?
                bool ridgesAboveAndBelow = AnyInColumn(blockHasRidges, column, 0, row)
                                           && AnyInColumn(blockHasRidges, column, row, blockRows - 1);
                blockInMask[column, row] = ridgesLeftAndRight && ridgesAboveAndBelow;
            }
        }

        // ---------- KROK 5: z kwadracików robimy wynik dla każdego piksela ----------
        // Każdy piksel dostaje odpowiedź swojego kwadracika.
        // Kwadracik piksela = współrzędne podzielone przez 8, np. piksel (20, 13) -> kwadracik (2, 1).
        // y * width + x = pozycja piksela w tablicy (wiersze ułożone jeden za drugim).
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

    // Odchylenie standardowe = "o ile średnio piksele różnią się od średniej jasności".
    // Przykład dla pikseli 40, 200, 40, 200:
    //   średnia = (40 + 200 + 40 + 200) / 4 = 120
    //   różnice od średniej: -80, 80, -80, 80  -> do kwadratu: 6400 każda -> suma 25600
    //   wynik = pierwiastek(25600 / 4) = 80   -> dużo, kwadracik jest pasiasty
    private static double BlockStdDev(byte[] grayscale, int width, int height, int left, int top)
    {
        // prawy i dolny brzeg kwadracika (Math.Min - żeby ostatni kwadracik nie wyszedł poza zdjęcie)
        int right = Math.Min(width, left + BlockSize);
        int bottom = Math.Min(height, top + BlockSize);
        int pixelCount = (right - left) * (bottom - top);   // zwykle 8 * 8 = 64

        // 1. średnia jasność w kwadraciku
        double sum = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                sum += grayscale[y * width + x];
            }
        }
        double mean = sum / pixelCount;

        // 2. suma kwadratów różnic od średniej (kwadrat, żeby minusy nie znosiły się z plusami)
        double squaredDifferences = 0;
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                double difference = grayscale[y * width + x] - mean;
                squaredDifferences += difference * difference;
            }
        }

        // 3. średnia z kwadratów i pierwiastek
        return Math.Sqrt(squaredDifferences / pixelCount);
    }

    // czy w danym wierszu kwadracików, między kolumnami fromColumn i toColumn, jest choć jeden z palcem?
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

    // czy w danej kolumnie kwadracików, między wierszami fromRow i toRow, jest choć jeden z palcem?
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
