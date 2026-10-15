namespace Segmentation.Core;

/// <summary>Magic-wand selection over opaque or straight-alpha BGRA32 pixels.</summary>
public static class MagicWand
{
    public static byte[] Select(byte[] bgra, int width, int height, int x, int y,
        int tolerance = 30, int maxPixels = 0, bool global = false)
    {
        int count = ValidateDimensions(width, height);
        ArgumentNullException.ThrowIfNull(bgra);
        if (bgra.LongLength != (long)count * 4)
            throw new ArgumentException("Expected width × height × 4 BGRA bytes.", nameof(bgra));
        if (x < 0 || x >= width) throw new ArgumentOutOfRangeException(nameof(x));
        if (y < 0 || y >= height) throw new ArgumentOutOfRangeException(nameof(y));
        if (tolerance is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if (maxPixels < 0) throw new ArgumentOutOfRangeException(nameof(maxPixels));

        int seed = y * width + x;
        int limit = maxPixels == 0 ? count : Math.Min(maxPixels, count);
        var selection = new byte[count];

        int Distance(int pixel)
        {
            int offset = pixel * 4, reference = seed * 4;
            return Math.Max(Math.Abs(bgra[offset] - bgra[reference]),
                Math.Max(Math.Abs(bgra[offset + 1] - bgra[reference + 1]),
                    Math.Abs(bgra[offset + 2] - bgra[reference + 2])));
        }

        if (global)
        {
            var buckets = new List<int>?[tolerance + 1];
            for (int i = 0; i < count; i++)
            {
                int distance = Distance(i);
                if (distance <= tolerance) (buckets[distance] ??= []).Add(i);
            }
            int selected = 0;
            foreach (var bucket in buckets)
            {
                if (bucket is null) continue;
                foreach (int pixel in bucket)
                {
                    selection[pixel] = 1;
                    if (++selected == limit) return selection;
                }
            }
        }
        else
        {
            var seen = new bool[count];
            var queue = new Queue<int>();
            queue.Enqueue(seed);
            seen[seed] = true;
            int selected = 0;
            while (queue.TryDequeue(out int pixel) && selected < limit)
            {
                selection[pixel] = 1;
                selected++;
                int px = pixel % width, py = pixel / width;
                // Visit four-connected neighbours in right, left, down, up order.
                if (px + 1 < width) Visit(pixel + 1);
                if (px > 0) Visit(pixel - 1);
                if (py + 1 < height) Visit(pixel + width);
                if (py > 0) Visit(pixel - width);
            }

            void Visit(int pixel)
            {
                if (seen[pixel]) return;
                seen[pixel] = true;
                if (Distance(pixel) <= tolerance) queue.Enqueue(pixel);
            }
        }
        return selection;
    }

    /// <summary>Full 3×3 neighbourhood, with background outside the image.</summary>
    public static byte[] Morph(byte[] selection, int width, int height, bool dilate)
    {
        int count = ValidateDimensions(width, height);
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Length != count)
            throw new ArgumentException("Selection dimensions do not match.", nameof(selection));
        var result = new byte[count];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool any = false, all = true;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                bool value = nx >= 0 && ny >= 0 && nx < width && ny < height
                    && selection[ny * width + nx] != 0;
                any |= value;
                all &= value;
            }
            result[y * width + x] = (byte)((dilate ? any : all) ? 1 : 0);
        }
        return result;
    }

    private static int ValidateDimensions(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        return checked(width * height);
    }
}
