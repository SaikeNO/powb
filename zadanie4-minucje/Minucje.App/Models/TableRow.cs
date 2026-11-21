using System.Windows.Media;

namespace Minucje.Models;

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

    public object? VisibilityKey { get; init; }

    public bool IsVisible { get; init; }

    public bool HasCheckBox => VisibilityKey != null;
}
