using System.Diagnostics;
using Szkieletyzacja.Core;

namespace Szkieletyzacja.Tests;

public class ThinningTests
{
    public static IEnumerable<object[]> AllAlgorithms()
    {
        foreach (ThinningAlgorithm algorithm in Enum.GetValues<ThinningAlgorithm>())
        {
            yield return new object[] { algorithm };
        }
    }

    private static TestImage Thin(ThinningAlgorithm algorithm, TestImage image)
    {
        byte[] thinnedPixels = Thinning.Run(algorithm, image.Pixels, image.PaddedWidth, image.PaddedHeight);
        return TestImage.WithPixels(thinnedPixels, image);
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void EmptyImage_StaysEmpty(ThinningAlgorithm algorithm)
    {
        var image = new TestImage(10, 10);

        Assert.Equal(0, Thin(algorithm, image).CountObjectPixels());
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void InputImage_IsNotModified(ThinningAlgorithm algorithm)
    {
        TestImage image = TestImage.FilledRectangle(20, 20, 3, 3, 12, 12);
        var pixelsBefore = (byte[])image.Pixels.Clone();

        Thinning.Run(algorithm, image.Pixels, image.PaddedWidth, image.PaddedHeight);

        Assert.Equal(pixelsBefore, image.Pixels);
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void SinglePixel_IsKept(ThinningAlgorithm algorithm)
    {
        var image = new TestImage(5, 5);
        image[2, 2] = 1;

        Assert.Equal(1, Thin(algorithm, image).CountObjectPixels());
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void OnePixelLine_IsUnchanged(ThinningAlgorithm algorithm)
    {
        TestImage image = TestImage.FromAscii(
            ".........",
            ".#######.",
            ".........");

        TestImage skeleton = Thin(algorithm, image);

        Assert.Equal(image.ToAscii(), skeleton.ToAscii());
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void ThickHorizontalLine_BecomesOnePixelWide(ThinningAlgorithm algorithm)
    {
        TestImage image = TestImage.FilledRectangle(40, 11, 2, 3, 36, 5);

        TestImage skeleton = Thin(algorithm, image);

        Assert.Equal(1, skeleton.CountConnectedComponents());
        for (int x = 8; x < 32; x++)
        {
            int skeletonPixelsInColumn = 0;
            for (int y = 0; y < skeleton.Height; y++)
            {
                skeletonPixelsInColumn += skeleton[x, y];
            }
            Assert.Equal(1, skeletonPixelsInColumn);
        }
        Assert.True(skeleton.CountObjectPixels() >= 30, skeleton.ToAscii());
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void ThickPlus_StaysConnected_AndOnlyRemovesPixels(ThinningAlgorithm algorithm)
    {
        var image = new TestImage(41, 41);
        for (int y = 0; y < 41; y++)
        {
            for (int x = 0; x < 41; x++)
            {
                if (Math.Abs(x - 20) <= 3 || Math.Abs(y - 20) <= 3)
                {
                    image[x, y] = 1;
                }
            }
        }

        TestImage skeleton = Thin(algorithm, image);

        Assert.Equal(1, skeleton.CountConnectedComponents());
        Assert.True(skeleton.CountObjectPixels() > 0);
        Assert.True(skeleton.CountObjectPixels() < image.CountObjectPixels() / 5);
        for (int pixelIndex = 0; pixelIndex < image.Pixels.Length; pixelIndex++)
        {
            Assert.True(skeleton.Pixels[pixelIndex] <= image.Pixels[pixelIndex]);
        }
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void TwoSeparateObjects_StayTwoObjects(ThinningAlgorithm algorithm)
    {
        var image = new TestImage(50, 20);
        for (int y = 5; y < 14; y++)
        {
            for (int x = 3; x < 20; x++)
            {
                image[x, y] = 1;
            }
            for (int x = 30; x < 47; x++)
            {
                image[x, y] = 1;
            }
        }

        Assert.Equal(2, Thin(algorithm, image).CountConnectedComponents());
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void Ring_KeepsItsHole(ThinningAlgorithm algorithm)
    {
        var image = new TestImage(41, 41);
        for (int y = 0; y < 41; y++)
        {
            for (int x = 0; x < 41; x++)
            {
                double distanceFromCenter = Math.Sqrt((x - 20) * (x - 20) + (y - 20) * (y - 20));
                if (distanceFromCenter >= 10 && distanceFromCenter <= 16)
                {
                    image[x, y] = 1;
                }
            }
        }

        TestImage skeleton = Thin(algorithm, image);

        Assert.Equal(1, skeleton.CountConnectedComponents());
        Assert.Equal(0, skeleton[20, 20]);
        Assert.True(skeleton.CountObjectPixels() > 20);
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void ThinningTwice_DoesNotBreakTheSkeleton(ThinningAlgorithm algorithm)
    {
        TestImage image = TestImage.FilledRectangle(60, 30, 5, 5, 50, 15);

        TestImage skeletonAfterOnePass = Thin(algorithm, image);
        TestImage skeletonAfterTwoPasses = Thin(algorithm, skeletonAfterOnePass);

        if (algorithm == ThinningAlgorithm.K3M)
        {
            Assert.Equal(1, skeletonAfterTwoPasses.CountConnectedComponents());
            Assert.True(skeletonAfterTwoPasses.CountObjectPixels() >= skeletonAfterOnePass.CountObjectPixels() - 4);
        }
        else
        {
            Assert.Equal(skeletonAfterOnePass.ToAscii(), skeletonAfterTwoPasses.ToAscii());
        }
    }

    [Theory, MemberData(nameof(AllAlgorithms))]
    public void LargeImage_IsProcessedUnderOneSecond(ThinningAlgorithm algorithm)
    {
        const int imageSize = 1500;
        var image = new TestImage(imageSize, imageSize);
        var random = new Random(1);
        for (int rectangleIndex = 0; rectangleIndex < 120; rectangleIndex++)
        {
            int left = random.Next(imageSize - 80);
            int top = random.Next(imageSize - 80);
            int rectangleWidth = random.Next(10, 80);
            int rectangleHeight = random.Next(4, 20);
            for (int y = top; y < top + rectangleHeight; y++)
            {
                for (int x = left; x < left + rectangleWidth; x++)
                {
                    image[x, y] = 1;
                }
            }
        }

        Thinning.Run(algorithm, image.Pixels, image.PaddedWidth, image.PaddedHeight);
        var stopwatch = Stopwatch.StartNew();
        Thinning.Run(algorithm, image.Pixels, image.PaddedWidth, image.PaddedHeight);
        stopwatch.Stop();

        Assert.True(stopwatch.ElapsedMilliseconds < 1000, $"{algorithm}: {stopwatch.ElapsedMilliseconds} ms");
    }
}
