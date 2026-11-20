using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Minucje;

// zamiana obrazu WPF na tablicę jasności 0-255 i zapis PNG
public static class ImageConversion
{
    public static byte[] ToGrayscale(BitmapSource source, out int width, out int height)
    {
        var grayscale = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        width = grayscale.PixelWidth;
        height = grayscale.PixelHeight;

        var pixels = new byte[width * height];
        grayscale.CopyPixels(pixels, width, 0);
        return pixels;
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
