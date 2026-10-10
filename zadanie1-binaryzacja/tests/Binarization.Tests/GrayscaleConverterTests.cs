using Binarization.Core;
namespace Binarization.Tests;

public class GrayscaleConverterTests
{
    [Theory]
    [InlineData(0, 0, 0, 255, 0)]
    [InlineData(255, 255, 255, 255, 255)]
    [InlineData(0, 0, 255, 255, 76)]
    [InlineData(0, 255, 0, 255, 150)]
    [InlineData(255, 0, 0, 255, 29)]
    [InlineData(0, 0, 0, 0, 255)]
    [InlineData(255, 0, 255, 0, 255)]
    [InlineData(0, 0, 255, 128, 165)]
    [InlineData(0, 0, 0, 128, 127)]
    public void ConvertsKnownColorsAndAlpha(int b, int g, int r, int a, int expected)
    {
        byte[] input = [(byte)b, (byte)g, (byte)r, (byte)a];
        byte[] original = input.ToArray();
        var result = GrayscaleConverter.ConvertBgra32(1, 1, input);
        Assert.Equal((byte)expected, result.Gray.Pixels.Span[0]);
        Assert.Equal(original, input);
        Assert.Equal((byte)255, result.OpaqueBgra[3]);
        if (a == 0) Assert.Equal(new byte[] { 255, 255, 255, 255 }, result.OpaqueBgra);
        if (a == 255) Assert.Equal(original, result.OpaqueBgra);
        if (a == 128 && r == 255) Assert.Equal(new byte[] { 127, 127, 255, 255 }, result.OpaqueBgra);
    }

    [Fact]
    public void PreservesDimensionsAndPixelOrder()
    {
        var result = GrayscaleConverter.ConvertBgra32(2, 2,
            new byte[] { 0, 0, 0, 255, 255, 255, 255, 255, 0, 0, 255, 255, 255, 0, 0, 255 });
        Assert.Equal(2, result.Gray.Width); Assert.Equal(2, result.Gray.Height);
        Assert.Equal(new byte[] { 0, 255, 76, 29 }, result.Gray.Pixels.ToArray());
        Assert.Equal(16, result.OpaqueBgra.Length);
    }

    [Fact]
    public void RejectsInvalidDimensionsAndBufferLength()
    {
        Assert.Throws<ArgumentException>(() => GrayscaleConverter.ConvertBgra32(0, 1, []));
        Assert.Throws<ArgumentException>(() => GrayscaleConverter.ConvertBgra32(-1, 1, new byte[4]));
        Assert.Throws<ArgumentException>(() => GrayscaleConverter.ConvertBgra32(1, 0, []));
        Assert.Throws<ArgumentException>(() => GrayscaleConverter.ConvertBgra32(1, 1, new byte[3]));
        Assert.Throws<ArgumentException>(() => GrayscaleConverter.ConvertBgra32(1, 1, new byte[8]));
        Assert.Throws<ArgumentException>(() => GrayscaleConverter.ConvertBgra32(int.MaxValue, int.MaxValue, new byte[4]));
    }
}
