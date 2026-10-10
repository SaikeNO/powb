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

    /// <summary>Binaryzacja progowa do tablicy 0/1 z ramką 1 px (rozmiar (w+2)x(h+2)).</summary>
    public static byte[] BinarizePadded(byte[] gray, int w, int h, int threshold, bool darkObject)
    {
        int W = w + 2;
        var bin = new byte[W * (h + 2)];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool dark = gray[y * w + x] < threshold;
                if (dark == darkObject) bin[(y + 1) * W + x + 1] = 1;
            }
        return bin;
    }

    /// <summary>Rysuje tablicę 0/1 (z ramką): obiekt czarny, tło białe.</summary>
    public static BitmapSource ToBitmap(byte[] padded, int W, int H)
    {
        int w = W - 2, h = H - 2;
        var px = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = padded[(y + 1) * W + x + 1] == 1 ? (byte)0 : (byte)255;
        var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, px, w);
        bmp.Freeze();
        return bmp;
    }
}
