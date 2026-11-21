using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Minucje.Core.Analysis;
using Minucje.Core.Models;
using Minucje.Imaging;
using Minucje.Models;
using Minucje.Rendering;

namespace Minucje;

// okno: dwa odciski A i B obok siebie + tabela z liczbą minucji
// cała analiza jest w Minucje.Core, tu tylko obsługa kontrolek
public partial class MainWindow : Window
{
    private static readonly SampleImage[] Samples =
    {
        new("Odcisk 1_1", "fp_1_1.png"),
        new("Odcisk 1_2", "fp_1_2.png"),
        new("Odcisk 2_1", "fp_2_1.png"),
        new("Odcisk 2_2", "fp_2_2.png"),
        new("Odcisk 5_3", "fp_5_3.png"),
        new("Odcisk 5_4", "fp_5_4.png"),
        new("Odcisk 10_1", "fp_10_1.png"),
        new("Odcisk 10_2", "fp_10_2.png"),
        new("Odcisk 12_2", "fp_12_2.png"),
        new("Odcisk 12_3", "fp_12_3.png"),
    };

    private readonly FingerprintPanel _panelA;
    private readonly FingerprintPanel _panelB;

    private bool _respondToControlChanges;   // false = ignorujemy zdarzenia kontrolek (np. przy starcie)

    // co rysujemy na obrazach - sterowane checkboxami w tabelach
    private readonly HashSet<MinutiaType> _visibleMinutiaTypes = new(Enum.GetValues<MinutiaType>());
    private readonly HashSet<RejectionReason> _visibleRejectionReasons = new(Enum.GetValues<RejectionReason>());

    public MainWindow()
    {
        InitializeComponent();

        _panelA = new FingerprintPanel(PanelA, SampleComboA, ImageA, InfoA);
        _panelB = new FingerprintPanel(PanelB, SampleComboB, ImageB, InfoB);
        SampleComboA.ItemsSource = Samples;
        SampleComboB.ItemsSource = Samples;
        RejectedLegendIcon.Source = Legend.RejectedIcon();

        _respondToControlChanges = true;
        SampleComboA.SelectedIndex = 0;
        SampleComboB.SelectedIndex = 1;
    }

    private FingerprintPanel PanelFromTag(object sender)
    {
        return (string)((FrameworkElement)sender).Tag == "B" ? _panelB : _panelA;
    }

    private void Sample_Changed(object sender, SelectionChangedEventArgs e)
    {
        FingerprintPanel panel = PanelFromTag(sender);
        if (_respondToControlChanges && panel.SampleCombo.SelectedItem is SampleImage sample)
        {
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/Assets/" + sample.FileName));
            LoadImage(panel, bitmap, sample.Name);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
        if (dialog.ShowDialog() == true)
        {
            LoadFile(PanelFromTag(sender), dialog.FileName);
        }
    }

    private void Panel_DragEnter(object sender, DragEventArgs e)
    {
        PanelFromTag(sender).Border.BorderBrush = Brushes.RoyalBlue;
    }

    private void Panel_DragLeave(object sender, DragEventArgs e)
    {
        PanelFromTag(sender).Border.ClearValue(Border.BorderBrushProperty);
    }

    private void Panel_Drop(object sender, DragEventArgs e)
    {
        FingerprintPanel panel = PanelFromTag(sender);
        panel.Border.ClearValue(Border.BorderBrushProperty);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] droppedFiles)
        {
            LoadFile(panel, droppedFiles[0]);
        }
    }

    private void LoadFile(FingerprintPanel panel, string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        try
        {
            var bitmap = new BitmapImage(new Uri(filePath));

            _respondToControlChanges = false;
            panel.SampleCombo.SelectedIndex = -1;
            _respondToControlChanges = true;

            LoadImage(panel, bitmap, fileName);
        }
        catch (Exception)
        {
            StatusText.Text = $"Nie udało się wczytać {fileName}. Obsługiwane: PNG, JPG, BMP, TIFF.";
        }
    }

    // obraz -> tablica jasności -> analiza -> rysowanie -> tabela
    private void LoadImage(FingerprintPanel panel, BitmapSource bitmap, string imageName)
    {
        panel.ImageName = imageName;
        panel.Grayscale = ImageConversion.ToGrayscale(bitmap, out int width, out int height);
        panel.Width = width;
        panel.Height = height;

        Analyze(panel);
        UpdateTables();
    }

    private AnalysisOptions CurrentOptions()
    {
        return new AnalysisOptions(
            RemoveFalseMinutiae: RemoveFalseMinutiaeCheck.IsChecked == true,
            BorderMargin: (int)BorderMarginSlider.Value,
            MinDistance: (int)MinDistanceSlider.Value);
    }

    private ViewMode CurrentViewMode()
    {
        if (ViewSkeleton.IsChecked == true)
        {
            return ViewMode.Skeleton;
        }
        if (ViewBinary.IsChecked == true)
        {
            return ViewMode.Binary;
        }
        return ViewMode.Original;
    }

    // uruchamia cały algorytm z FingerprintAnalyzer dla jednego panelu
    private void Analyze(FingerprintPanel panel)
    {
        if (panel.Grayscale == null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        panel.Analysis = FingerprintAnalyzer.Analyze(panel.Grayscale, panel.Width, panel.Height, CurrentOptions());
        panel.AnalysisMilliseconds = stopwatch.ElapsedMilliseconds;

        Draw(panel);
    }

    private void Draw(FingerprintPanel panel)
    {
        if (panel.Analysis == null || panel.Grayscale == null)
        {
            return;
        }

        panel.ImageControl.Source = MinutiaeRenderer.Render(
            panel.Analysis, panel.Grayscale, CurrentViewMode(), _visibleMinutiaTypes, _visibleRejectionReasons);
        panel.InfoText.Text =
            $"{panel.Analysis.Minutiae.Count} minucji  ·  {panel.ImageName}  ·  {panel.Width}×{panel.Height} px";
    }

    // zmiana filtra -> trzeba policzyć minucje od nowa
    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_respondToControlChanges)
        {
            Analyze(_panelA);
            Analyze(_panelB);
            UpdateTables();
        }
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BorderMarginText != null && MinDistanceText != null)
        {
            BorderMarginText.Text = $"{BorderMarginSlider.Value} px";
            MinDistanceText.Text = $"{MinDistanceSlider.Value} px";
            Options_Changed(sender, e);
        }
    }

    // zmiana widoku lub checkboxa -> wystarczy narysować od nowa, bez analizy
    private void View_Changed(object sender, RoutedEventArgs e)
    {
        if (_respondToControlChanges)
        {
            Draw(_panelA);
            Draw(_panelB);
        }
    }

    private void Visibility_Changed(object sender, RoutedEventArgs e)
    {
        var checkBox = (CheckBox)sender;
        bool show = checkBox.IsChecked == true;
        bool changed = checkBox.Tag switch
        {
            MinutiaType type => show ? _visibleMinutiaTypes.Add(type) : _visibleMinutiaTypes.Remove(type),
            RejectionReason reason => show ? _visibleRejectionReasons.Add(reason) : _visibleRejectionReasons.Remove(reason),
            _ => false,
        };
        if (changed)
        {
            View_Changed(sender, e);
        }
    }

    // liczba minucji każdego typu dla A i B oraz liczba odrzuconych według przyczyny
    private void UpdateTables()
    {
        FingerprintAnalysis? analysisA = _panelA.Analysis;
        FingerprintAnalysis? analysisB = _panelB.Analysis;

        var minutiaRows = new List<TableRow>();
        foreach (MinutiaType type in Enum.GetValues<MinutiaType>())
        {
            minutiaRows.Add(new TableRow(Legend.MarkerIcon(type), Legend.NameOf(type), Legend.CrossingNumberOf(type),
                analysisA?.Count(type), analysisB?.Count(type), IsTotal: false, Legend.DescriptionOf(type))
            {
                VisibilityKey = type,
                IsVisible = _visibleMinutiaTypes.Contains(type),
            });
        }
        minutiaRows.Add(new TableRow(null, "Razem", "",
            analysisA?.Minutiae.Count, analysisB?.Minutiae.Count, IsTotal: true, null));
        MinutiaCountsList.ItemsSource = minutiaRows;

        bool filterEnabled = RemoveFalseMinutiaeCheck.IsChecked == true;
        ImageSource rejectedIcon = Legend.RejectedIcon();
        var rejectedRows = new List<TableRow>();
        foreach (RejectionReason reason in Enum.GetValues<RejectionReason>())
        {
            rejectedRows.Add(new TableRow(rejectedIcon, Legend.NameOf(reason), "",
                filterEnabled ? analysisA?.CountRejected(reason) : null,
                filterEnabled ? analysisB?.CountRejected(reason) : null,
                IsTotal: false, Legend.DescriptionOf(reason))
            {
                VisibilityKey = reason,
                IsVisible = _visibleRejectionReasons.Contains(reason),
            });
        }
        rejectedRows.Add(new TableRow(null, "Odrzucone razem", "",
            filterEnabled ? analysisA?.Rejected.Count : null,
            filterEnabled ? analysisB?.Rejected.Count : null,
            IsTotal: true, null));
        RejectedCountsList.ItemsSource = rejectedRows;

        StatusText.Text = $"Analiza: A {_panelA.AnalysisMilliseconds} ms, B {_panelB.AnalysisMilliseconds} ms. " +
                          "Przeciągnij obraz na panel A lub B, żeby go podmienić.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        FingerprintPanel panel = PanelFromTag(sender);
        if (panel.ImageControl.Source is not BitmapSource markedImage)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = Path.GetFileNameWithoutExtension(panel.ImageName).Replace(' ', '_') + "_minucje.png",
        };
        if (dialog.ShowDialog() == true)
        {
            ImageConversion.SavePng(markedImage, dialog.FileName);
            StatusText.Text = "Zapisano: " + dialog.FileName;
        }
    }
}
