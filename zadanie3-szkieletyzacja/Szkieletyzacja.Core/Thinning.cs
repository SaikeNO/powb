namespace Szkieletyzacja.Core;

public enum ThinningAlgorithm { ZhangSuen, GuoHall, Kmm, K3M }

/// <summary>
/// Algorytmy szkieletyzacji. Obraz: tablica 0/1 (1 = obiekt) z ramką 1 px tła dookoła,
/// więc sąsiadów można czytać bez sprawdzania granic.
/// Decyzje o usunięciu piksela zapadają w tablicach 256-elementowych indeksowanych maską
/// sąsiedztwa (jedno obliczenie maski na piksel, brak rozgałęzień w pętli).
/// </summary>
public static class Thinning
{
    // Sąsiedzi P2..P9 (zgodnie z ruchem wskazówek zegara, od góry) = bity 0..7
    // wagi: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128

    public static string Name(ThinningAlgorithm a) => a switch
    {
        ThinningAlgorithm.ZhangSuen => "Zhang-Suen",
        ThinningAlgorithm.GuoHall => "Guo-Hall",
        ThinningAlgorithm.Kmm => "KMM",
        ThinningAlgorithm.K3M => "K3M",
        _ => a.ToString()
    };

    public static byte[] Run(ThinningAlgorithm a, byte[] src, int W, int H) => a switch
    {
        ThinningAlgorithm.ZhangSuen => ZhangSuen(src, W, H),
        ThinningAlgorithm.GuoHall => GuoHall(src, W, H),
        ThinningAlgorithm.Kmm => Kmm(src, W, H),
        ThinningAlgorithm.K3M => K3M(src, W, H),
        _ => throw new ArgumentOutOfRangeException(nameof(a))
    };

    private static int Mask(byte[] img, int i, int W) =>
        img[i - W] | img[i - W + 1] << 1 | img[i + 1] << 2 | img[i + W + 1] << 3
      | img[i + W] << 4 | img[i + W - 1] << 5 | img[i - 1] << 6 | img[i - W - 1] << 7;

    private static bool Bit(int w, int bit) => (w >> bit & 1) == 1;

    private static int Transitions(int w)
    {
        int a = 0;
        for (int i = 0; i < 8; i++)
            if (!Bit(w, i) && Bit(w, (i + 1) & 7)) a++;
        return a;
    }

    private static int Count(int w)
    {
        int b = 0;
        for (int i = 0; i < 8; i++) if (Bit(w, i)) b++;
        return b;
    }

    // ---------- tablice decyzyjne ----------

    private static readonly bool[] ZsStep1 = BuildZs(0), ZsStep2 = BuildZs(1);
    private static readonly bool[] GhStep1 = BuildGh(0), GhStep2 = BuildGh(1);
    private static readonly bool[] Deletable = BuildKmm();

    private static bool[] BuildZs(int step)
    {
        var t = new bool[256];
        for (int w = 0; w < 256; w++)
        {
            int p2 = w & 1, p4 = w >> 2 & 1, p6 = w >> 4 & 1, p8 = w >> 6 & 1;
            int b = Count(w);
            if (b < 2 || b > 6 || Transitions(w) != 1) continue;
            t[w] = step == 0
                ? p2 * p4 * p6 == 0 && p4 * p6 * p8 == 0
                : p2 * p4 * p8 == 0 && p2 * p6 * p8 == 0;
        }
        return t;
    }

    private static bool[] BuildGh(int step)
    {
        var t = new bool[256];
        for (int w = 0; w < 256; w++)
        {
            bool p2 = Bit(w, 0), p3 = Bit(w, 1), p4 = Bit(w, 2), p5 = Bit(w, 3);
            bool p6 = Bit(w, 4), p7 = Bit(w, 5), p8 = Bit(w, 6), p9 = Bit(w, 7);
            int c = (!p2 && (p3 || p4) ? 1 : 0) + (!p4 && (p5 || p6) ? 1 : 0)
                  + (!p6 && (p7 || p8) ? 1 : 0) + (!p8 && (p9 || p2) ? 1 : 0);
            int n1 = ((p9 || p2) ? 1 : 0) + ((p3 || p4) ? 1 : 0) + ((p5 || p6) ? 1 : 0) + ((p7 || p8) ? 1 : 0);
            int n2 = ((p2 || p3) ? 1 : 0) + ((p4 || p5) ? 1 : 0) + ((p6 || p7) ? 1 : 0) + ((p8 || p9) ? 1 : 0);
            int n = Math.Min(n1, n2);
            if (c != 1 || n < 2 || n > 3) continue;
            t[w] = step == 0 ? !((p2 || p3 || !p5) && p4) : !((p6 || p7 || !p9) && p8);
        }
        return t;
    }

    private static bool[] BuildKmm()
    {
        var t = new bool[256];
        for (int w = 0; w < 256; w++)
        {
            int b = Count(w);
            // nie końcówka (B>=2), nie punkt wewnętrzny (B<=7), usunięcie nie rozspaja (A==1)
            t[w] = b >= 2 && b <= 7 && Transitions(w) == 1;
        }
        return t;
    }

    private static bool[] Set(params int[] weights)
    {
        var t = new bool[256];
        foreach (int w in weights) t[w] = true;
        return t;
    }

    // K3M (Saeed i in. 2010): piksele brzegowe oraz zbiory wag usuwanych w kolejnych fazach
    private static readonly bool[] K3MBorder = Set(3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56, 60, 62, 63, 96, 112, 120, 124, 126, 127,
        129, 131, 135, 143, 159, 191, 192, 193, 195, 199, 207, 223, 224, 225, 227, 231, 239, 240, 241, 243, 247, 248, 249, 251, 252, 253, 254);

    private static readonly bool[][] K3MPhases =
    {
        Set(7, 14, 28, 56, 112, 131, 193, 224),
        Set(7, 14, 15, 28, 30, 56, 60, 112, 120, 131, 135, 193, 195, 224, 225, 240),
        Set(7, 14, 15, 28, 30, 31, 56, 60, 62, 112, 120, 124, 131, 135, 143, 193, 195, 199, 224, 225, 227, 240, 241, 248),
        Set(7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 193, 195, 199, 207, 224, 225, 227, 231, 240, 241, 243, 248, 249, 252),
        Set(7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 191, 193, 195, 199, 207, 224, 225, 227, 231, 239, 240, 241, 243, 248, 249, 251, 252, 254),
    };

    // ---------- algorytmy równoległe (Zhang-Suen, Guo-Hall) ----------

    private static byte[] TwoSubIteration(byte[] src, int W, int H, bool[] t1, bool[] t2)
    {
        var img = (byte[])src.Clone();
        var del = new int[img.Length];
        var tables = new[] { t1, t2 };
        bool changed;
        do
        {
            changed = false;
            foreach (var t in tables)
            {
                int n = 0;
                for (int y = 1; y < H - 1; y++)
                {
                    int i = y * W + 1;
                    for (int x = 1; x < W - 1; x++, i++)
                        if (img[i] != 0 && t[Mask(img, i, W)]) del[n++] = i;
                }
                for (int k = 0; k < n; k++) img[del[k]] = 0;
                if (n > 0) changed = true;
            }
        } while (changed);
        return img;
    }

    public static byte[] ZhangSuen(byte[] src, int W, int H) => TwoSubIteration(src, W, H, ZsStep1, ZsStep2);

    public static byte[] GuoHall(byte[] src, int W, int H) => TwoSubIteration(src, W, H, GhStep1, GhStep2);

    // ---------- KMM (wariant) ----------

    /// <summary>
    /// KMM (wariant): w każdej iteracji najpierw usuwane są piksele brzegowe 4-spójne,
    /// potem narożne. O usunięciu decyduje tablica Deletable[waga sąsiedztwa],
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
                        if (Deletable[Mask(img, i, W)])
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

    // ---------- K3M ----------

    /// <summary>
    /// K3M: w każdej iteracji oznaczane są piksele brzegowe (zbiór K3MBorder), potem w 5 fazach
    /// usuwane są oznaczone piksele o wagach z kolejnych zbiorów (na bieżącym obrazie).
    /// </summary>
    public static byte[] K3M(byte[] src, int W, int H)
    {
        var img = (byte[])src.Clone();
        var border = new int[img.Length];
        bool changed;
        do
        {
            changed = false;
            int n = 0;
            for (int y = 1; y < H - 1; y++)
            {
                int i = y * W + 1;
                for (int x = 1; x < W - 1; x++, i++)
                    if (img[i] != 0 && K3MBorder[Mask(img, i, W)]) border[n++] = i;
            }

            foreach (var phase in K3MPhases)
                for (int k = 0; k < n; k++)
                {
                    int i = border[k];
                    if (img[i] != 0 && phase[Mask(img, i, W)])
                    {
                        img[i] = 0;
                        changed = true;
                    }
                }
        } while (changed);
        return img;
    }
}
