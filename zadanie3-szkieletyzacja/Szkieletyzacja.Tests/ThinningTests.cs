using Szkieletyzacja.Core;

namespace Szkieletyzacja.Tests;

public class ThinningTests
{
    public static IEnumerable<object[]> Algorithms()
    {
        yield return new object[] { "ZhangSuen", (Func<byte[], int, int, byte[]>)Thinning.ZhangSuen };
        yield return new object[] { "GuoHall", (Func<byte[], int, int, byte[]>)Thinning.GuoHall };
        yield return new object[] { "KMM", (Func<byte[], int, int, byte[]>)Thinning.Kmm };
        yield return new object[] { "K3M", (Func<byte[], int, int, byte[]>)Thinning.K3M };
    }

    private static Img Run(Func<byte[], int, int, byte[]> algo, Img img) =>
        Img.FromData(algo(img.Data, img.W, img.H), img);

    [Theory, MemberData(nameof(Algorithms))]
    public void EmptyImage_StaysEmpty(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = new Img(10, 10);
        Assert.Equal(0, Run(algo, img).Count());
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void DoesNotModifyInput(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = Img.FilledRect(20, 20, 3, 3, 12, 12);
        var copy = (byte[])img.Data.Clone();
        algo(img.Data, img.W, img.H);
        Assert.Equal(copy, img.Data);
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void SinglePixel_IsKept(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = new Img(5, 5);
        img[2, 2] = 1;
        Assert.Equal(1, Run(algo, img).Count());
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void OnePixelLine_IsUnchanged(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = Img.FromAscii(
            ".........",
            ".#######.",
            ".........");
        var res = Run(algo, img);
        Assert.Equal(img.ToAscii(), res.ToAscii());
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void ThickHorizontalLine_BecomesThin_AndKeepsMostOfLength(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = Img.FilledRect(40, 11, 2, 3, 36, 5);   // 36x5
        var res = Run(algo, img);

        Assert.Equal(1, res.Components());
        // w środkowych kolumnach dokładnie 1 piksel szkieletu
        for (int x = 8; x < 32; x++)
        {
            int inColumn = 0;
            for (int y = 0; y < res.Height; y++) inColumn += res[x, y];
            Assert.Equal(1, inColumn);
        }
        Assert.True(res.Count() >= 30, $"szkielet za krótki: {res.Count()}\n{res.ToAscii()}");
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void ThickPlus_KeepsConnectivity_AndIsSubsetOfInput(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = new Img(41, 41);
        for (int y = 0; y < 41; y++)
            for (int x = 0; x < 41; x++)
                if (Math.Abs(x - 20) <= 3 || Math.Abs(y - 20) <= 3) img[x, y] = 1;

        var res = Run(algo, img);

        Assert.Equal(1, res.Components());
        Assert.True(res.Count() > 0);
        Assert.True(res.Count() < img.Count() / 5, "szkielet powinien być dużo mniejszy od obiektu");
        for (int i = 0; i < img.Data.Length; i++)
            Assert.True(res.Data[i] <= img.Data[i], "szkielet nie może dodawać pikseli");
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void TwoSeparateObjects_StayTwo(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = new Img(50, 20);
        for (int y = 5; y < 14; y++)
        {
            for (int x = 3; x < 20; x++) img[x, y] = 1;
            for (int x = 30; x < 47; x++) img[x, y] = 1;
        }
        Assert.Equal(2, Run(algo, img).Components());
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void Ring_KeepsTheHole(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = new Img(41, 41);
        for (int y = 0; y < 41; y++)
            for (int x = 0; x < 41; x++)
            {
                double d = Math.Sqrt((x - 20) * (x - 20) + (y - 20) * (y - 20));
                if (d >= 10 && d <= 16) img[x, y] = 1;
            }

        var res = Run(algo, img);
        Assert.Equal(1, res.Components());
        Assert.Equal(0, res[20, 20]);
        Assert.True(res.Count() > 20);
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void Result_IsStable_WhenThinnedAgain(string name, Func<byte[], int, int, byte[]> algo)
    {
        var img = Img.FilledRect(60, 30, 5, 5, 50, 15);
        var once = Run(algo, img);
        var twice = Run(algo, once);
        if (name == "K3M")
        {
            // faza A1pix z artykulu skraca konce linii, wiec ponowne przejscie moze zabrac pojedyncze piksele
            Assert.Equal(1, twice.Components());
            for (int i = 0; i < once.Data.Length; i++) Assert.True(twice.Data[i] <= once.Data[i]);
            Assert.True(twice.Count() >= once.Count() - 4);
            return;
        }
        Assert.Equal(once.ToAscii(), twice.ToAscii());
    }

    [Theory, MemberData(nameof(Algorithms))]
    public void LargeImage_IsProcessedUnderOneSecond(string name, Func<byte[], int, int, byte[]> algo)
    {
        const int n = 1500;
        var img = new Img(n, n);
        var rnd = new Random(1);
        for (int k = 0; k < 120; k++)
        {
            int x0 = rnd.Next(n - 80), y0 = rnd.Next(n - 80), w = rnd.Next(10, 80), h = rnd.Next(4, 20);
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++) img[x, y] = 1;
        }

        algo(img.Data, img.W, img.H);   // rozgrzewka JIT
        var sw = System.Diagnostics.Stopwatch.StartNew();
        algo(img.Data, img.W, img.H);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 1000, $"{name}: {sw.ElapsedMilliseconds} ms");
    }
}
