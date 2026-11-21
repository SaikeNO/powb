namespace Minucje.Core.Analysis;

public sealed record AnalysisOptions(
    bool RemoveFalseMinutiae = true,
    int BorderMargin = 16,
    int MinDistance = 8);
