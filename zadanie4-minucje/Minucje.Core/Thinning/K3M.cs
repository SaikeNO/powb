using Minucje.Core.Imaging;

namespace Minucje.Core.Thinning;

public static class K3M
{
    private static readonly int[] BorderWeights =
    {
        3, 6, 7, 12, 14, 15, 24, 28, 30, 31, 48, 56, 60, 62, 63, 96, 112, 120, 124, 126, 127, 129, 131, 135, 143, 159,
        191, 192, 193, 195, 199, 207, 223, 224, 225, 227, 231, 239, 240, 241, 243, 247, 248, 249, 251, 252, 253, 254,
    };

    private static readonly int[][] PhaseWeights =
    {
        new[] { 7, 14, 28, 56, 112, 131, 193, 224 },
        new[] { 7, 14, 15, 28, 30, 56, 60, 112, 120, 131, 135, 193, 195, 224, 225, 240 },
        new[]
        {
            7, 14, 15, 28, 30, 31, 56, 60, 62, 112, 120, 124, 131, 135, 143, 193, 195, 199, 224, 225, 227, 240, 241, 248,
        },
        new[]
        {
            7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 193, 195, 199, 207, 224, 225,
            227, 231, 240, 241, 243, 248, 249, 252,
        },
        new[]
        {
            7, 14, 15, 28, 30, 31, 56, 60, 62, 63, 112, 120, 124, 126, 131, 135, 143, 159, 191, 193, 195, 199, 207, 224,
            225, 227, 231, 239, 240, 241, 243, 248, 249, 251, 252, 254,
        },
    };

    private static readonly bool[] IsBorder = ToLookup(BorderWeights);
    private static readonly bool[][] CanDeleteInPhase = PhaseWeights.Select(ToLookup).ToArray();

    private static readonly bool[] CanDeleteInFinalPass = IsBorder;

    public static byte[] Thin(byte[] binaryImage, int paddedWidth, int paddedHeight)
    {
        var skeleton = (byte[])binaryImage.Clone();
        var borderPixels = new List<int>();
        bool anyPixelDeleted = true;

        while (anyPixelDeleted)
        {
            anyPixelDeleted = false;

            borderPixels.Clear();
            for (int y = 1; y < paddedHeight - 1; y++)
            {
                for (int x = 1; x < paddedWidth - 1; x++)
                {
                    int pixelIndex = y * paddedWidth + x;
                    if (skeleton[pixelIndex] == 1 && IsBorder[Neighbourhood.GetWeight(skeleton, pixelIndex, paddedWidth)])
                    {
                        borderPixels.Add(pixelIndex);
                    }
                }
            }

            foreach (bool[] canDelete in CanDeleteInPhase)
            {
                foreach (int pixelIndex in borderPixels)
                {
                    if (skeleton[pixelIndex] == 1 && canDelete[Neighbourhood.GetWeight(skeleton, pixelIndex, paddedWidth)])
                    {
                        skeleton[pixelIndex] = 0;
                        anyPixelDeleted = true;
                    }
                }
            }
        }

        for (int y = 1; y < paddedHeight - 1; y++)
        {
            for (int x = 1; x < paddedWidth - 1; x++)
            {
                int pixelIndex = y * paddedWidth + x;
                if (skeleton[pixelIndex] == 1 && CanDeleteInFinalPass[Neighbourhood.GetWeight(skeleton, pixelIndex, paddedWidth)])
                {
                    skeleton[pixelIndex] = 0;
                }
            }
        }

        return skeleton;
    }

    private static bool[] ToLookup(int[] weights)
    {
        var lookup = new bool[256];
        foreach (int weight in weights)
        {
            lookup[weight] = true;
        }
        return lookup;
    }
}
