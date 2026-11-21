using Minucje.Core.Analysis;
using Minucje.Core.Minutiae;
using Minucje.Core.Models;

namespace Minucje.Tests;

public class FilterAndPipelineTests
{
    private const int Width = 100;
    private const int Height = 100;

    private static readonly bool[] FullMask = Enumerable.Repeat(true, Width * Height).ToArray();

    [Fact]
    public void MinutiaNearImageEdge_IsRejectedAsScannerBorder()
    {
        var nearEdge = new Minutia(3, 50, MinutiaType.Ending, 1);
        var inside = new Minutia(50, 50, MinutiaType.Ending, 1);

        List<RejectedMinutia> rejected = MinutiaeFilter.Apply(
            new List<Minutia> { nearEdge, inside }, FullMask, Width, Height, borderMargin: 10, minDistance: 5);

        RejectedMinutia single = Assert.Single(rejected);
        Assert.Equal(nearEdge, single.Minutia);
        Assert.Equal(RejectionReason.ScannerBorder, single.Reason);
    }

    [Fact]
    public void MinutiaNearMaskEdge_IsRejected()
    {
        bool[] mask = (bool[])FullMask.Clone();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 60; x < Width; x++)
            {
                mask[y * Width + x] = false;
            }
        }

        Assert.True(MinutiaeFilter.IsNearBorder(new Minutia(55, 50, MinutiaType.Ending, 1), mask, Width, Height, 10));
        Assert.False(MinutiaeFilter.IsNearBorder(new Minutia(40, 50, MinutiaType.Ending, 1), mask, Width, Height, 10));
    }

    [Theory]
    [InlineData(MinutiaType.Ending, MinutiaType.Ending, RejectionReason.BrokenRidge)]
    [InlineData(MinutiaType.Ending, MinutiaType.Bifurcation, RejectionReason.Spur)]
    [InlineData(MinutiaType.Bifurcation, MinutiaType.Bifurcation, RejectionReason.Bridge)]
    public void ClosePair_IsRejectedTogether(MinutiaType first, MinutiaType second, RejectionReason expected)
    {
        var firstMinutia = new Minutia(50, 50, first, 1);
        var secondMinutia = new Minutia(53, 52, second, 3);
        var farAway = new Minutia(80, 80, MinutiaType.Ending, 1);

        List<RejectedMinutia> rejected = MinutiaeFilter.Apply(
            new List<Minutia> { firstMinutia, secondMinutia, farAway }, FullMask, Width, Height, borderMargin: 5, minDistance: 6);

        Assert.Equal(2, rejected.Count);
        Assert.All(rejected, item => Assert.Equal(expected, item.Reason));
        Assert.DoesNotContain(rejected, item => item.Minutia == farAway);
    }

    [Fact]
    public void Analyze_SyntheticRidges_KeepsOnlyRealMinutiae()
    {
        const int width = 160;
        const int height = 160;
        var grayscale = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isRidge = y % 10 < 4;
                bool isGap = y / 10 == 7 && x >= 60 && x < 100;
                bool isBridge = x >= 110 && x < 114 && y >= 100 && y < 110;
                grayscale[y * width + x] = (isRidge && !isGap) || isBridge ? (byte)30 : (byte)230;
            }
        }

        FingerprintAnalysis withoutFilter = FingerprintAnalyzer.Analyze(
            grayscale, width, height, new AnalysisOptions(RemoveFalseMinutiae: false));
        FingerprintAnalysis withFilter = FingerprintAnalyzer.Analyze(
            grayscale, width, height, new AnalysisOptions(BorderMargin: 12, MinDistance: 6));

        Assert.True(withoutFilter.Count(MinutiaType.Ending) > 20);

        Assert.Equal(2, withFilter.Count(MinutiaType.Ending));
        Assert.Equal(2, withFilter.Count(MinutiaType.Bifurcation));
        Assert.Contains(withFilter.Rejected, item => item.Reason == RejectionReason.ScannerBorder);
    }
}
