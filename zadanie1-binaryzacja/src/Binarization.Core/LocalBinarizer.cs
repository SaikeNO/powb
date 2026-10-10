namespace Binarization.Core;

/// <summary>Square clipped neighborhoods with integral mean and population variance.</summary>
public abstract class LocalBinarizer : BinarizationAlgorithm
{
    protected sealed override byte[] Process(GrayImage image, Parameters p, out int? globalThreshold)
    {
        globalThreshold = null;
        var input = image.Pixels.Span;
        var output = new byte[input.Length];
        int w = image.Width, h = image.Height, stride = checked(w + 1);
        int size = checked(stride * checked(h + 1));
        var sums = new double[size];
        var squares = new double[size];
        for (int y = 0; y < h; y++)
        {
            double row = 0, rowSquares = 0;
            for (int x = 0; x < w; x++)
            {
                double v = input[y * w + x];
                row += v; rowSquares += v * v;
                int i = (y + 1) * stride + x + 1;
                sums[i] = sums[i - stride] + row;
                squares[i] = squares[i - stride] + rowSquares;
            }
        }
        int radius = p.WindowSize / 2;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int left = Math.Max(0, x - radius), top = Math.Max(0, y - radius);
            int right = (int)Math.Min(w, (long)x + radius + 1), bottom = (int)Math.Min(h, (long)y + radius + 1);
            double count = (double)(right - left) * (bottom - top);
            double m = Rectangle(sums, stride, left, top, right, bottom) / count;
            double s = Math.Sqrt(Math.Max(0, Rectangle(squares, stride, left, top, right, bottom) / count - m * m));
            double t = CalculateThreshold(m, s, p);
            if (!double.IsFinite(t)) throw new ArgumentException("Parameters produce a nonfinite threshold. Use smaller values.");
            output[y * w + x] = input[y * w + x] > t ? (byte)255 : (byte)0;
        }
        return output;
    }

    protected abstract double CalculateThreshold(double mean, double standardDeviation, Parameters parameters);

    private static double Rectangle(double[] a, int stride, int l, int t, int r, int b) =>
        a[b * stride + r] - a[t * stride + r] - a[b * stride + l] + a[t * stride + l];

}
