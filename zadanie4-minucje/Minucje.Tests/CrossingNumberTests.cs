using Minucje.Core;

namespace Minucje.Tests;

public class CrossingNumberTests
{
    // obraz z napisów ('#' = linia) -> tablica 0/1 z ramką 1 piksela
    public static (byte[] Pixels, int PaddedWidth, int PaddedHeight) FromAscii(params string[] rows)
    {
        int paddedWidth = rows[0].Length + 2;
        int paddedHeight = rows.Length + 2;
        var pixels = new byte[paddedWidth * paddedHeight];
        for (int y = 0; y < rows.Length; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] == '#')
                {
                    pixels[(y + 1) * paddedWidth + (x + 1)] = 1;
                }
            }
        }
        return (pixels, paddedWidth, paddedHeight);
    }

    private static int CrossingNumberOfCentre(params string[] rows)
    {
        (byte[] pixels, int paddedWidth, _) = FromAscii(rows);
        int centre = 2 * paddedWidth + 2; // piksel (1, 1) obrazu 3×3
        return CrossingNumber.Compute(pixels, centre, paddedWidth);
    }

    private static List<Minutia> Find(params string[] rows)
    {
        (byte[] pixels, int paddedWidth, int paddedHeight) = FromAscii(rows);
        return CrossingNumber.FindMinutiae(pixels, paddedWidth, paddedHeight);
    }

    [Theory]
    [InlineData(new[] { "...", ".#.", "..." }, 0)] // brak sąsiadów
    [InlineData(new[] { ".#.", ".#.", "..." }, 1)] // jeden sąsiad
    [InlineData(new[] { ".##", ".#.", "..." }, 1)] // dwaj sąsiedzi obok siebie to nadal jedna linia
    [InlineData(new[] { ".#.", ".#.", ".#." }, 2)] // linia przechodzi przez piksel
    [InlineData(new[] { "...", "###", ".#." }, 3)] // trzy gałęzie
    [InlineData(new[] { ".#.", "###", ".#." }, 4)] // cztery gałęzie
    public void Compute_CountsLinesLeavingPixel(string[] rows, int expected)
    {
        Assert.Equal(expected, CrossingNumberOfCentre(rows));
    }

    [Fact]
    public void StraightLine_HasTwoEndings()
    {
        List<Minutia> minutiae = Find(
            ".......",
            ".#####.",
            ".......");

        Assert.Equal(2, minutiae.Count);
        Assert.All(minutiae, m => Assert.Equal(MinutiaType.Ending, m.Type));
        Assert.Contains(minutiae, m => m.X == 1 && m.Y == 1);
        Assert.Contains(minutiae, m => m.X == 5 && m.Y == 1);
    }

    [Fact]
    public void YShape_HasOneBifurcationAndThreeEndings()
    {
        List<Minutia> minutiae = Find(
            "#.....#",
            ".#...#.",
            "..#.#..",
            "...#...",
            "...#...",
            "...#...");

        Minutia bifurcation = Assert.Single(minutiae, m => m.Type == MinutiaType.Bifurcation);
        Assert.Equal((3, 3), (bifurcation.X, bifurcation.Y));
        Assert.Equal(3, minutiae.Count(m => m.Type == MinutiaType.Ending));
    }

    [Fact]
    public void XShape_HasOneCrossingAndFourEndings()
    {
        List<Minutia> minutiae = Find(
            "#...#",
            ".#.#.",
            "..#..",
            ".#.#.",
            "#...#");

        Minutia crossing = Assert.Single(minutiae, m => m.Type == MinutiaType.Crossing);
        Assert.Equal((2, 2), (crossing.X, crossing.Y));
        Assert.Equal(4, minutiae.Count(m => m.Type == MinutiaType.Ending));
    }

    [Fact]
    public void SinglePixel_IsIsolatedPoint()
    {
        Minutia minutia = Assert.Single(Find(
            "...",
            ".#.",
            "..."));

        Assert.Equal(MinutiaType.Isolated, minutia.Type);
    }

    [Fact]
    public void ClosedRing_HasNoMinutiae()
    {
        Assert.Empty(Find(
            ".###.",
            "#...#",
            "#...#",
            ".###."));
    }
}
