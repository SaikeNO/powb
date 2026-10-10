using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Binarization.Core;
namespace Binarization.App;

public static class ImageFiles
{
    public static (BitmapSource Original, GrayImage Gray) Load(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight, stride = checked(w * 4);
        var bgra = new byte[checked(stride * h)];
        converted.CopyPixels(bgra, stride, 0);
        var gray = new byte[checked(w * h)];
        for (int i = 0; i < gray.Length; i++)
        {
            int j = i * 4;
            double a = bgra[j + 3] / 255.0;
            double b = bgra[j] * a + 255 * (1 - a), g = bgra[j + 1] * a + 255 * (1 - a), r = bgra[j + 2] * a + 255 * (1 - a);
            gray[i] = (byte)Math.Clamp(Math.Round(0.299 * r + 0.587 * g + 0.114 * b), 0, 255);
            bgra[j] = (byte)Math.Round(b); bgra[j + 1] = (byte)Math.Round(g); bgra[j + 2] = (byte)Math.Round(r); bgra[j + 3] = 255;
        }
        var original = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, stride);
        original.Freeze(); return (original, new GrayImage(w, h, gray));
    }
    public static BitmapSource Preview(GrayImage image)
    {
        var bitmap = BitmapSource.Create(image.Width, image.Height, 96, 96, PixelFormats.Gray8, null, image.Pixels.ToArray(), image.Width);
        bitmap.Freeze(); return bitmap;
    }
    public static void Save(GrayImage image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Preview(image)));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
