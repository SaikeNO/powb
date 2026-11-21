using Minucje.Core.Analysis;
using Minucje.Core.Minutiae;
using Minucje.Core.Models;

namespace Minucje.Tests;

public class CrossingNumberTests
{
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
        int centre = 2 * paddedWidth + 2;
        return CrossingNumber.Compute(pixels, centre, paddedWidth);
    }

    private static List<Minutia> Find(params string[] rows)
    {
        (byte[] pixels, int paddedWidth, int paddedHeight) = FromAscii(rows);
        return CrossingNumber.FindMinutiae(pixels, paddedWidth, paddedHeight);
    }

    [Theory]
    [InlineData(new[] { "...", ".#.", "..." }, 0)]
    [InlineData(new[] { ".#.", ".#.", "..." }, 1)]
    [InlineData(new[] { ".##", ".#.", "..." }, 1)]
    [InlineData(new[] { ".#.", ".#.", ".#." }, 2)]
    [InlineData(new[] { "...", "###", ".#." }, 3)]
    [InlineData(new[] { ".#.", "###", ".#." }, 4)]
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
        Assert.All(minutiae, minutia => Assert.Equal(MinutiaType.Ending, minutia.Type));
        Assert.Contains(minutiae, minutia => minutia.X == 1 && minutia.Y == 1);
        Assert.Contains(minutiae, minutia => minutia.X == 5 && minutia.Y == 1);
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

        Minutia bifurcation = Assert.Single(minutiae, minutia => minutia.Type == MinutiaType.Bifurcation);
        Assert.Equal((3, 3), (bifurcation.X, bifurcation.Y));
        Assert.Equal(3, minutiae.Count(minutia => minutia.Type == MinutiaType.Ending));
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

        Minutia crossing = Assert.Single(minutiae, minutia => minutia.Type == MinutiaType.Crossing);
        Assert.Equal((2, 2), (crossing.X, crossing.Y));
        Assert.Equal(4, minutiae.Count(minutia => minutia.Type == MinutiaType.Ending));
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
