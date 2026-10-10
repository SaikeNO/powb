namespace Binarization.Core;

public sealed class NiblackBinarizer : LocalBinarizer
{
    public override Algorithm Algorithm => Algorithm.Niblack;

    protected override double CalculateThreshold(double m, double s, Parameters p) =>
        m + p.NiblackK * s;
}
