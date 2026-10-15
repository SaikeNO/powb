using Segmentation.Core;

namespace Segmentation.Tests;

public class MagicWandTests
{
    private static byte[] Gray(params byte[] values) =>
        values.SelectMany(v => new byte[] { v, v, v, 255 }).ToArray();

    [Fact]
    public void LocalSelectionUsesFourConnectedNeighboursWithoutRowWrapping()
    {
        byte[] pixels = Gray(0, 255, 0, 255, 0, 0);
        Assert.Equal(new byte[] { 1, 0, 0, 0, 0, 0 },
            MagicWand.Select(pixels, 3, 2, 0, 0, tolerance: 0));
        Assert.Equal(new byte[] { 1, 0, 1, 0, 1, 1 },
            MagicWand.Select(pixels, 3, 2, 0, 0, tolerance: 0, global: true));
    }

    [Fact]
    public void DistanceUsesMaximumRgbDifferenceAndInclusiveToleranceIgnoringAlpha()
    {
        byte[] pixels = [10, 20, 30, 255, 40, 50, 60, 0, 10, 20, 61, 255];
        Assert.Equal(new byte[] { 1, 1, 0 },
            MagicWand.Select(pixels, 3, 1, 0, 0, tolerance: 30, global: true));
    }

    [Fact]
    public void LocalToleranceAlwaysComparesAgainstSeed()
    {
        Assert.Equal(new byte[] { 1, 1, 0 },
            MagicWand.Select(Gray(0, 20, 40), 3, 1, 0, 0, tolerance: 30));
    }

    [Fact]
    public void LocalCapUsesBreadthFirstRightLeftDownUpOrder()
    {
        Assert.Equal(new byte[] { 0, 0, 0, 1, 1, 1, 0, 0, 0 },
            MagicWand.Select(Gray(new byte[9]), 3, 3, 1, 1, maxPixels: 3));
    }

    [Fact]
    public void GlobalCapPrefersClosestColourThenRasterOrder()
    {
        Assert.Equal(new byte[] { 0, 1, 0, 1, 1 },
            MagicWand.Select(Gray(120, 101, 99, 100, 100), 5, 1, 3, 0,
                tolerance: 30, maxPixels: 3, global: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZeroCapIsUnlimitedAndTolerance255IncludesEveryColour(bool global)
    {
        Assert.Equal(new byte[] { 1, 1, 1 },
            MagicWand.Select(Gray(0, 128, 255), 3, 1, 0, 0, 255, 0, global));
    }

    [Fact]
    public void MorphologyIncludesDiagonalsAndTreatsOutsideAsBackground()
    {
        byte[] center = [0, 0, 0, 0, 1, 0, 0, 0, 0];
        byte[] full = Enumerable.Repeat((byte)1, 9).ToArray();
        Assert.Equal(full, MagicWand.Morph(center, 3, 3, dilate: true));
        Assert.Equal(center, MagicWand.Morph(full, 3, 3, dilate: false));
        Assert.Equal(new byte[9], MagicWand.Morph(center, 3, 3, dilate: false));
        Assert.Equal(new byte[] { 0 }, MagicWand.Morph([1], 1, 1, dilate: false));
    }

    [Fact]
    public void OperationsReturnNewMasksAndPreserveInputs()
    {
        byte[] pixels = Gray(0, 0, 255);
        byte[] original = (byte[])pixels.Clone();
        byte[] mask = MagicWand.Select(pixels, 3, 1, 0, 0);
        byte[] maskCopy = (byte[])mask.Clone();
        MagicWand.Morph(mask, 3, 1, true);
        Assert.Equal(original, pixels);
        Assert.Equal(maskCopy, mask);
        Assert.Equal(new byte[] { 0, 0, 1 }, MagicWand.Select(pixels, 3, 1, 2, 0));
    }

    [Fact]
    public void InvalidParametersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => MagicWand.Select([0], 1, 1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicWand.Select(Gray(0), 1, 1, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicWand.Select(Gray(0), 1, 1, 0, 0, 256));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicWand.Select(Gray(0), 1, 1, 0, 0, maxPixels: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MagicWand.Morph([], 0, 1, true));
        Assert.Throws<ArgumentException>(() => MagicWand.Morph([], 1, 1, true));
    }
}
