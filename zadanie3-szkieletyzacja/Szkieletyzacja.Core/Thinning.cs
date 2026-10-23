namespace Szkieletyzacja.Core;

public enum ThinningAlgorithm { ZhangSuen, GuoHall, Kmm, K3M }

public static class Thinning
{

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

    private static readonly bool[] ZsStep1 = BuildZs(0), ZsStep2 = BuildZs(1);
    private static readonly bool[] GhStep1 = BuildGh(0), GhStep2 = BuildGh(1);

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

    private static bool[] Set(params int[] weights)
    {
        var t = new bool[256];
        foreach (int w in weights) t[w] = true;
        return t;
    }

    private static readonly bool[] KmmDeletion = Set(3, 5, 7, 12, 13, 14, 15, 20, 21, 22, 23, 28, 29, 30, 31, 48, 52, 53, 54, 55, 56, 60, 61, 62, 63, 65, 67, 69, 71, 77, 79, 80, 81, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 94, 95, 97, 99, 101, 103, 109, 111, 112, 113, 115, 116, 117, 118, 119, 120, 121, 123, 124, 125, 126, 127, 131, 133, 135, 141, 143, 149, 151, 157, 159, 181, 183, 189, 191, 192, 193, 195, 197, 199, 205, 207, 208, 209, 211, 212, 213, 214, 215, 216, 217, 219, 220, 221, 222, 223, 224, 225, 227, 229, 231, 237, 239, 240, 241, 243, 244, 245, 246, 247, 248, 249, 251, 252, 253, 254, 255);

    private static readonly bool[] KmmFour = Set(3, 6, 12, 24, 48, 96, 192, 129, 7, 14, 28, 56, 112, 224, 193, 131,
        15, 30, 60, 120, 240, 225, 195, 135);

    private static readonly bool[] K3MBorder = Set(3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56, 60, 62, 63, 96, 112, 120, 124, 126, 127,
        129, 131, 135, 143, 159, 191, 192, 193, 195, 199, 207, 223, 224, 225, 227, 231, 239, 240, 241, 243, 247, 248, 249, 251, 252, 253, 254);

    private static readonly bool[] K3MOnePixel = Set(3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56, 60, 62, 63, 96, 112, 120, 124, 126, 127,
        129, 131, 135, 143, 159, 191, 192, 193, 195, 199, 207, 223, 224, 225, 227, 231, 239, 240, 241, 243, 247, 248, 249, 251, 252, 253, 254);

    private static readonly bool[][] K3MPhases =
    {
        Set(7, 14, 28, 56, 112, 131, 193, 224),
        Set(7, 14, 15, 28, 30, 56, 60, 112, 120, 131, 135, 193, 195, 224, 225, 240),
        Set(7, 14, 15, 28, 30, 31, 56, 60, 62, 112, 120, 124, 131, 135, 143, 193, 195, 199, 224, 225, 227, 240, 241, 248),
        Set(7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 193, 195, 199, 207, 224, 225, 227, 231, 240, 241, 243, 248, 249, 252),
        Set(7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 191, 193, 195, 199, 207, 224, 225, 227, 231, 239, 240, 241, 243, 248, 249, 251, 252, 254),
    };

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

    private static int MaskNz(byte[] img, int i, int W) =>
        (img[i - W] + 3 >> 2) | (img[i - W + 1] + 3 >> 2) << 1 | (img[i + 1] + 3 >> 2) << 2 | (img[i + W + 1] + 3 >> 2) << 3
      | (img[i + W] + 3 >> 2) << 4 | (img[i + W - 1] + 3 >> 2) << 5 | (img[i - 1] + 3 >> 2) << 6 | (img[i - W - 1] + 3 >> 2) << 7;

    public static byte[] Kmm(byte[] src, int W, int H)
    {
        var img = (byte[])src.Clone();
        var contour = new int[img.Length];
        bool changed;
        do
        {
            changed = false;

            int n = 0;
            for (int y = 1; y < H - 1; y++)
            {
                int i = y * W + 1;
                for (int x = 1; x < W - 1; x++, i++)
                {
                    if (img[i] == 0) continue;
                    if (img[i - W] == 0 || img[i + 1] == 0 || img[i + W] == 0 || img[i - 1] == 0) img[i] = 2;
                    else if (img[i - W + 1] == 0 || img[i + W + 1] == 0 || img[i + W - 1] == 0 || img[i - W - 1] == 0) img[i] = 3;
                    else continue;
                    contour[n++] = i;
                }
            }

            int fours = 0;
            for (int k = 0; k < n; k++)
            {
                int i = contour[k];
                if (KmmFour[MaskNz(img, i, W)]) contour[fours++] = i;
            }
            for (int k = 0; k < fours; k++) img[contour[k]] = 0;
            if (fours > 0) changed = true;

            for (int label = 2; label <= 3; label++)
                for (int y = 1; y < H - 1; y++)
                {
                    int i = y * W + 1;
                    for (int x = 1; x < W - 1; x++, i++)
                    {
                        if (img[i] != label) continue;
                        if (KmmDeletion[MaskNz(img, i, W)]) { img[i] = 0; changed = true; }
                        else img[i] = 1;
                    }
                }

        } while (changed);
        return img;
    }

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

        for (int y = 1; y < H - 1; y++)
        {
            int i = y * W + 1;
            for (int x = 1; x < W - 1; x++, i++)
                if (img[i] != 0 && K3MOnePixel[Mask(img, i, W)]) img[i] = 0;
        }
        return img;
    }
}
