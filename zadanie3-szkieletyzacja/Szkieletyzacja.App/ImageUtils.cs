using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Szkieletyzacja;

public static class ImageUtils
{
    private const byte ObjectOnPreview = 0;
    private const byte BackgroundOnPreview = 255;
    private const byte NonSkeletonObjectOnOverlay = 170;

    public static byte[] ToGrayscale(BitmapSource source, out int width, out int height)
    {
        var grayscale = new FormatConvertedBitmap(source, PixelFormats.Gray8, null, 0);
        width = grayscale.PixelWidth;
        height = grayscale.PixelHeight;

        var pixels = new byte[width * height];
        grayscale.CopyPixels(pixels, width, 0);
        return pixels;
    }

    public static int ComputeOtsuThreshold(byte[] grayscale)
    {
        var histogram = new long[256];
        foreach (byte brightness in grayscale)
        {
            histogram[brightness]++;
        }

        double totalPixels = grayscale.Length;
        double totalBrightnessSum = 0;
        for (int brightness = 0; brightness < 256; brightness++)
        {
            totalBrightnessSum += brightness * (double)histogram[brightness];
        }

        double darkBrightnessSum = 0;
        double darkPixels = 0;
        double bestBetweenClassVariance = -1;
        int bestThreshold = 128;

        for (int brightness = 0; brightness < 256; brightness++)
        {
            darkPixels += histogram[brightness];
            if (darkPixels == 0)
            {
                continue;
            }

            double brightPixels = totalPixels - darkPixels;
            if (brightPixels == 0)
            {
                break;
            }

            darkBrightnessSum += brightness * (double)histogram[brightness];
            double darkMean = darkBrightnessSum / darkPixels;
            double brightMean = (totalBrightnessSum - darkBrightnessSum) / brightPixels;
            double meanDifference = darkMean - brightMean;
            double betweenClassVariance = darkPixels * brightPixels * meanDifference * meanDifference;

            if (betweenClassVariance > bestBetweenClassVariance)
            {
                bestBetweenClassVariance = betweenClassVariance;
                bestThreshold = brightness + 1;
            }
        }

        return bestThreshold;
    }

    public static byte[] BinarizeWithPadding(byte[] grayscale, int width, int height, int threshold, bool isObjectDark)
    {
        int paddedWidth = width + 2;
        var binary = new byte[paddedWidth * (height + 2)];

        for (int y = 0; y < height; y++)
        {
            int sourceIndex = y * width;
            int targetIndex = (y + 1) * paddedWidth + 1;
            for (int x = 0; x < width; x++)
            {
                bool isDark = grayscale[sourceIndex + x] < threshold;
                if (isDark == isObjectDark)
                {
                    binary[targetIndex + x] = 1;
                }
            }
        }

        return binary;
    }

    public static int CountObjectPixels(byte[] paddedImage)
    {
        int count = 0;
        foreach (byte pixel in paddedImage)
        {
            count += pixel;
        }
        return count;
    }

    public static BitmapSource ToBitmap(byte[] paddedImage, int paddedWidth, int paddedHeight)
    {
        int width = paddedWidth - 2;
        int height = paddedHeight - 2;
        var pixels = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            int sourceIndex = (y + 1) * paddedWidth + 1;
            int targetIndex = y * width;
            for (int x = 0; x < width; x++)
            {
                pixels[targetIndex + x] = paddedImage[sourceIndex + x] == 1 ? ObjectOnPreview : BackgroundOnPreview;
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
        bitmap.Freeze();
        return bitmap;
    }

    public static BitmapSource ToOverlay(
        byte[] paddedBinary, byte[] paddedSkeleton, int paddedWidth, int paddedHeight, Color skeletonColor)
    {
        int width = paddedWidth - 2;
        int height = paddedHeight - 2;
        var pixels = new byte[width * height * 4];

        for (int y = 0; y < height; y++)
        {
            int sourceIndex = (y + 1) * paddedWidth + 1;
            int targetIndex = y * width * 4;
            for (int x = 0; x < width; x++, targetIndex += 4)
            {
                byte blue, green, red;
                if (paddedSkeleton[sourceIndex + x] == 1)
                {
                    blue = skeletonColor.B;
                    green = skeletonColor.G;
                    red = skeletonColor.R;
                }
                else if (paddedBinary[sourceIndex + x] == 1)
                {
                    blue = green = red = NonSkeletonObjectOnOverlay;
                }
                else
                {
                    blue = green = red = BackgroundOnPreview;
                }

                pixels[targetIndex] = blue;
                pixels[targetIndex + 1] = green;
                pixels[targetIndex + 2] = red;
                pixels[targetIndex + 3] = 255;
            }
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    public static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
