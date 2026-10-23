using System.Text;

namespace Szkieletyzacja.Tests;

public sealed class TestImage
{
    public TestImage(int width, int height)
    {
        PaddedWidth = width + 2;
        PaddedHeight = height + 2;
        Pixels = new byte[PaddedWidth * PaddedHeight];
    }

    private TestImage(int width, int height, byte[] pixels)
    {
        PaddedWidth = width + 2;
        PaddedHeight = height + 2;
        Pixels = pixels;
    }

    public byte[] Pixels { get; }

    public int PaddedWidth { get; }

    public int PaddedHeight { get; }

    public int Width
    {
        get
        {
            return PaddedWidth - 2;
        }
    }

    public int Height
    {
        get
        {
            return PaddedHeight - 2;
        }
    }

    public byte this[int x, int y]
    {
        get
        {
            return Pixels[(y + 1) * PaddedWidth + x + 1];
        }
        set
        {
            Pixels[(y + 1) * PaddedWidth + x + 1] = value;
        }
    }

    public static TestImage FromAscii(params string[] rows)
    {
        var image = new TestImage(rows[0].Length, rows.Length);
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] == '#')
                {
                    image[x, y] = 1;
                }
            }
        }
        return image;
    }

    public static TestImage WithPixels(byte[] pixels, TestImage sameSizeAs)
    {
        return new TestImage(sameSizeAs.Width, sameSizeAs.Height, pixels);
    }

    public static TestImage FilledRectangle(
        int width, int height, int left, int top, int rectangleWidth, int rectangleHeight)
    {
        var image = new TestImage(width, height);
        for (int y = top; y < top + rectangleHeight; y++)
        {
            for (int x = left; x < left + rectangleWidth; x++)
            {
                image[x, y] = 1;
            }
        }
        return image;
    }

    public int CountObjectPixels()
    {
        return Pixels.Count(pixel => pixel == 1);
    }

    public int CountConnectedComponents()
    {
        var visited = new bool[Pixels.Length];
        var pixelsToVisit = new Stack<int>();
        int componentCount = 0;

        for (int startIndex = 0; startIndex < Pixels.Length; startIndex++)
        {
            if (Pixels[startIndex] == 0 || visited[startIndex])
            {
                continue;
            }

            componentCount++;
            visited[startIndex] = true;
            pixelsToVisit.Push(startIndex);

            while (pixelsToVisit.Count > 0)
            {
                int pixelIndex = pixelsToVisit.Pop();
                for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
                {
                    for (int columnOffset = -1; columnOffset <= 1; columnOffset++)
                    {
                        int neighbourIndex = pixelIndex + rowOffset * PaddedWidth + columnOffset;
                        bool isOutside = neighbourIndex < 0 || neighbourIndex >= Pixels.Length;
                        if (isOutside || Pixels[neighbourIndex] == 0 || visited[neighbourIndex])
                        {
                            continue;
                        }

                        visited[neighbourIndex] = true;
                        pixelsToVisit.Push(neighbourIndex);
                    }
                }
            }
        }

        return componentCount;
    }

    public string ToAscii()
    {
        var builder = new StringBuilder();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                builder.Append(this[x, y] == 1 ? '#' : '.');
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
