using Minucje.Core.Models;

namespace Minucje.Core.Analysis;

// wszystko, co program pokazuje dla jednego odcisku
public sealed class FingerprintAnalysis
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required bool[] Mask { get; init; }          // width × height, true = odcisk

    public required byte[] Binary { get; init; }        // 0/1 z ramką 1 piksela

    public required byte[] Skeleton { get; init; }      // 0/1 z ramką 1 piksela

    public required List<Minutia> Detected { get; init; }       // wszystko, co znalazł Crossing Number

    public required List<Minutia> Minutiae { get; init; }       // minucje po odrzuceniu fałszywych

    public required List<RejectedMinutia> Rejected { get; init; }

    public int Count(MinutiaType type)
    {
        return Minutiae.Count(minutia => minutia.Type == type);
    }

    public int CountRejected(RejectionReason reason)
    {
        return Rejected.Count(rejected => rejected.Reason == reason);
    }
}
