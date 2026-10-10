namespace Binarization.Core;

public sealed class SauvolaBinarizer : LocalBinarizer
{
    public override Algorithm Algorithm => Algorithm.Sauvola;

    protected override double CalculateThreshold(double m, double s, Parameters p) =>
        m * (1 + p.SauvolaK * (s / p.SauvolaR - 1));
}
