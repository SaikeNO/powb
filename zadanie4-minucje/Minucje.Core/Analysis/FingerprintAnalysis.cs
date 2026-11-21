using Minucje.Core.Models;

namespace Minucje.Core.Analysis;

public sealed class FingerprintAnalysis
{
    public required int Width { get; init; }

    public required int Height { get; init; }

    public required bool[] Mask { get; init; }

    public required byte[] Binary { get; init; }

    public required byte[] Skeleton { get; init; }

    public required List<Minutia> Detected { get; init; }

    public required List<Minutia> Minutiae { get; init; }

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
