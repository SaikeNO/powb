using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Szkieletyzacja;

public static class ImageUtils
{
    public static byte[] ToGray(BitmapSource src, out int w, out int h)
    {
        var gray = new FormatConvertedBitmap(src, PixelFormats.Gray8, null, 0);
        w = gray.PixelWidth; h = gray.PixelHeight;
        var px = new byte[w * h];
        gray.CopyPixels(px, w, 0);
        return px;
    }

    public static int Otsu(byte[] gray)
    {
        var hist = new long[256];
        foreach (byte g in gray) hist[g]++;
        double total = gray.Length, sum = 0;
        for (int i = 0; i < 256; i++) sum += i * (double)hist[i];
        double sumB = 0, wB = 0, best = -1;
        int threshold = 128;
        for (int t = 0; t < 256; t++)
        {
            wB += hist[t];
            if (wB == 0) continue;
            double wF = total - wB;
            if (wF == 0) break;
            sumB += t * (double)hist[t];
            double d = sumB / wB - (sum - sumB) / wF;
            double between = wB * wF * d * d;
            if (between > best) { best = between; threshold = t + 1; }
        }
        return threshold;
    }

    public static byte[] BinarizePadded(byte[] gray, int w, int h, int threshold, bool darkObject)
    {
        int W = w + 2;
        var bin = new byte[W * (h + 2)];
        for (int y = 0; y < h; y++)
        {
            int src = y * w, dst = (y + 1) * W + 1;
            for (int x = 0; x < w; x++)
                if ((gray[src + x] < threshold) == darkObject) bin[dst + x] = 1;
        }
        return bin;
    }

    public static int CountObject(byte[] padded)
    {
        int n = 0;
        foreach (byte b in padded) n += b;
        return n;
    }

    public static BitmapSource ToBitmap(byte[] padded, int W, int H)
    {
        int w = W - 2, h = H - 2;
        var px = new byte[w * h];
        for (int y = 0; y < h; y++)
        {
            int src = (y + 1) * W + 1, dst = y * w;
            for (int x = 0; x < w; x++)
                px[dst + x] = padded[src + x] == 1 ? (byte)0 : (byte)255;
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, px, w);
        bmp.Freeze();
        return bmp;
    }

    public static BitmapSource ToOverlay(byte[] binary, byte[] skeleton, int W, int H, Color skeletonColor)
    {
        int w = W - 2, h = H - 2;
        var px = new byte[w * h * 4];
        for (int y = 0; y < h; y++)
        {
            int src = (y + 1) * W + 1, dst = y * w * 4;
            for (int x = 0; x < w; x++, dst += 4)
            {
                byte b, g, r;
                if (skeleton[src + x] == 1) { b = skeletonColor.B; g = skeletonColor.G; r = skeletonColor.R; }
                else if (binary[src + x] == 1) { b = g = r = 170; }
                else { b = g = r = 255; }
                px[dst] = b; px[dst + 1] = g; px[dst + 2] = r; px[dst + 3] = 255;
            }
        }
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
        bmp.Freeze();
        return bmp;
    }

    public static void SavePng(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
