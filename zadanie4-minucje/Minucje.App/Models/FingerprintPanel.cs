using System.Windows.Controls;
using Minucje.Core.Analysis;

namespace Minucje.Models;

public sealed class FingerprintPanel
{
    public FingerprintPanel(Border border, ComboBox sampleCombo, Image imageControl, TextBlock infoText)
    {
        Border = border;
        SampleCombo = sampleCombo;
        ImageControl = imageControl;
        InfoText = infoText;
    }

    public Border Border { get; }

    public ComboBox SampleCombo { get; }

    public Image ImageControl { get; }

    public TextBlock InfoText { get; }

    public string ImageName { get; set; } = "";

    public byte[]? Grayscale { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public FingerprintAnalysis? Analysis { get; set; }

    public long AnalysisMilliseconds { get; set; }
}
