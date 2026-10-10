using System.Text;

namespace Szkieletyzacja.Tests;

/// <summary>Obraz 0/1 z ramką 1 px tła (tak jak oczekują algorytmy). Indeksator używa współrzędnych bez ramki.</summary>
public sealed class Img
{
    public byte[] Data { get; }
    public int W { get; }   // szerokość z ramką
    public int H { get; }   // wysokość z ramką
    public int Width => W - 2;
    public int Height => H - 2;

    public Img(int w, int h)
    {
        W = w + 2; H = h + 2;
        Data = new byte[W * H];
    }

    private Img(int w, int h, byte[] data)
    {
        W = w + 2; H = h + 2;
        Data = data;
    }

    public byte this[int x, int y]
    {
        get => Data[(y + 1) * W + x + 1];
        set => Data[(y + 1) * W + x + 1] = value;
    }

    /// <summary>'#' = obiekt, cokolwiek innego = tło.</summary>
    public static Img FromAscii(params string[] rows)
    {
        var img = new Img(rows[0].Length, rows.Length);
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '#') img[x, y] = 1;
        return img;
    }

    public static Img FromData(byte[] data, Img like) => new(like.Width, like.Height, data);

    public static Img FilledRect(int w, int h, int x0, int y0, int rw, int rh)
    {
        var img = new Img(w, h);
        for (int y = y0; y < y0 + rh; y++)
            for (int x = x0; x < x0 + rw; x++)
                img[x, y] = 1;
        return img;
    }

    public int Count() => Data.Count(p => p == 1);

    /// <summary>Liczba składowych 8-spójnych obiektu.</summary>
    public int Components()
    {
        var seen = new bool[Data.Length];
        int comps = 0;
        var stack = new Stack<int>();
        for (int s = 0; s < Data.Length; s++)
        {
            if (Data[s] == 0 || seen[s]) continue;
            comps++;
            seen[s] = true;
            stack.Push(s);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int j = i + dy * W + dx;
                        if (j < 0 || j >= Data.Length || Data[j] == 0 || seen[j]) continue;
                        seen[j] = true;
                        stack.Push(j);
                    }
            }
        }
        return comps;
    }

    public string ToAscii()
    {
        var sb = new StringBuilder();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++) sb.Append(this[x, y] == 1 ? '#' : '.');
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
