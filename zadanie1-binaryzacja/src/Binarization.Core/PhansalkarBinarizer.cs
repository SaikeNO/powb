namespace Binarization.Core;

public sealed class PhansalkarBinarizer : LocalBinarizer
{
    public override Algorithm Algorithm => Algorithm.Phansalkar;

    protected override double CalculateThreshold(double m, double s, Parameters p)
    {
        // Phansalkar operates on normalized intensities; return threshold in byte scale.
        m /= 255;
        s /= 255;
        return 255 * m * (1 + p.P * Math.Exp(-p.Q * m) + p.PhansalkarK * (s / p.PhansalkarR - 1));
    }
}
