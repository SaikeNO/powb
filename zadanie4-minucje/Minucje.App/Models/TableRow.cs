using System.Windows.Media;

namespace Minucje.Models;

// Jeden wiersz tabeli po prawej (liczba minucji danego typu albo odrzuconych z danej przyczyny).
// null w kolumnie A/B wyświetla się jako "—".
public sealed record TableRow(
    ImageSource? Icon,
    string Name,
    string CrossingNumber,
    int? CountA,
    int? CountB,
    bool IsTotal,
    string? ToolTip)
{
    public string CountAText => CountA?.ToString() ?? "—";

    public string CountBText => CountB?.ToString() ?? "—";

    // typ minucji albo przyczyna odrzucenia; tylko takie wiersze mają checkbox "pokaż na obrazie"
    public object? VisibilityKey { get; init; }

    public bool IsVisible { get; init; }

    public bool HasCheckBox => VisibilityKey != null;
}
