using System.Collections.Generic;

namespace Szkieletyzacja.Core;

/// <summary>
/// Algorytmy szkieletyzacji. Obraz: tablica 0/1 (1 = obiekt) z ramką 1 px tła dookoła,
/// więc sąsiadów można czytać bez sprawdzania granic.
/// </summary>
public static class Thinning
{
    // Sąsiedzi P2..P9 (zgodnie z ruchem wskazówek zegara, od góry) = bity 0..7
    // wagi KMM: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128
    private static readonly bool[] Deletable = BuildDeletableTable();

    private static bool[] BuildDeletableTable()
    {
        var t = new bool[256];
        for (int w = 0; w < 256; w++)
        {
            int b = 0, a = 0;
            for (int i = 0; i < 8; i++)
            {
                bool cur = (w >> i & 1) == 1;
                bool next = (w >> ((i + 1) % 8) & 1) == 1;
                if (cur) b++;
                if (!cur && next) a++;
            }
            // nie końcówka (B>=2), nie punkt wewnętrzny (B<=7), usunięcie nie rozspaja (A==1)
            t[w] = b >= 2 && b <= 7 && a == 1;
        }
        return t;
    }

    public static byte[] ZhangSuen(byte[] src, int W, int H)
    {
        var img = (byte[])src.Clone();
        var toDelete = new List<int>();
        bool changed;
        do
        {
            changed = false;
            for (int step = 0; step < 2; step++)
            {
                toDelete.Clear();
                for (int y = 1; y < H - 1; y++)
                {
                    int i = y * W + 1;
                    for (int x = 1; x < W - 1; x++, i++)
                    {
                        if (img[i] == 0) continue;
                        int p2 = img[i - W], p3 = img[i - W + 1], p4 = img[i + 1], p5 = img[i + W + 1];
                        int p6 = img[i + W], p7 = img[i + W - 1], p8 = img[i - 1], p9 = img[i - W - 1];

                        int b = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9;
                        if (b < 2 || b > 6) continue;

                        int a = 0;
                        if (p2 == 0 && p3 == 1) a++;
                        if (p3 == 0 && p4 == 1) a++;
                        if (p4 == 0 && p5 == 1) a++;
                        if (p5 == 0 && p6 == 1) a++;
                        if (p6 == 0 && p7 == 1) a++;
                        if (p7 == 0 && p8 == 1) a++;
                        if (p8 == 0 && p9 == 1) a++;
                        if (p9 == 0 && p2 == 1) a++;
                        if (a != 1) continue;

                        if (step == 0)
                        {
                            if (p2 * p4 * p6 != 0 || p4 * p6 * p8 != 0) continue;
                        }
                        else
                        {
                            if (p2 * p4 * p8 != 0 || p2 * p6 * p8 != 0) continue;
                        }
                        toDelete.Add(i);
                    }
                }
                foreach (int i in toDelete) img[i] = 0;
                if (toDelete.Count > 0) changed = true;
            }
        } while (changed);
        return img;
    }

    /// <summary>
    /// KMM (wariant): w każdej iteracji najpierw usuwane są piksele brzegowe 4-spójne (klasa "2"),
    /// potem narożne (klasa "3"). O usunięciu decyduje tablica Deletable[waga sąsiedztwa],
    /// a waga liczona jest na bieżącym obrazie (usuwanie sekwencyjne zachowuje spójność).
    /// </summary>
    public static byte[] Kmm(byte[] src, int W, int H)
    {
        var img = (byte[])src.Clone();
        bool changed;
        do
        {
            changed = false;
            for (int phase = 0; phase < 2; phase++)
            {
                for (int y = 1; y < H - 1; y++)
                {
                    int i = y * W + 1;
                    for (int x = 1; x < W - 1; x++, i++)
                    {
                        if (img[i] == 0) continue;
                        bool border4 = img[i - W] == 0 || img[i + 1] == 0 || img[i + W] == 0 || img[i - 1] == 0;
                        if (border4 != (phase == 0)) continue;

                        int w = img[i - W] | img[i - W + 1] << 1 | img[i + 1] << 2 | img[i + W + 1] << 3
                              | img[i + W] << 4 | img[i + W - 1] << 5 | img[i - 1] << 6 | img[i - W - 1] << 7;
                        if (Deletable[w])
                        {
                            img[i] = 0;
                            changed = true;
                        }
                    }
                }
            }
        } while (changed);
        return img;
    }
}
