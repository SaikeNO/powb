namespace Minucje.Core.Analysis;

// ustawienia filtra fałszywych minucji (suwaki w aplikacji)
public sealed record AnalysisOptions(
    bool RemoveFalseMinutiae = true,
    int BorderMargin = 16,
    int MinDistance = 8);
