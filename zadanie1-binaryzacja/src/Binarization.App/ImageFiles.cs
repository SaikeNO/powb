using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Binarization.Core;
namespace Binarization.App;

public sealed record LoadedImage(BitmapSource Original, int Width, int Height, byte[] Bgra);

public static class ImageFiles
{
    public static LoadedImage Load(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight, stride = checked(w * 4);
        var bgra = new byte[checked(stride * h)];
        converted.CopyPixels(bgra, stride, 0);
        var original = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, stride);
        original.Freeze(); return new(original, w, h, bgra);
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
