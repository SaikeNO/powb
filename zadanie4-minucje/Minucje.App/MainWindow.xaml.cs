using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Minucje.Core;

namespace Minucje;

// Okno: dwa odciski (A i B) obok siebie i tabela z liczbą minucji.
// Cała analiza obrazu jest w Minucje.Core (FingerprintAnalyzer), tutaj jest tylko obsługa przycisków.
public partial class MainWindow : Window
{
    private static readonly Sample[] Samples =
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
    private bool _isReady;

    // co jest rysowane na obrazie (checkboxy w tabelach); na start wszystko
    private readonly HashSet<MinutiaType> _visibleTypes = new(Enum.GetValues<MinutiaType>());
    private readonly HashSet<RejectionReason> _visibleReasons = new(Enum.GetValues<RejectionReason>());

    public MainWindow()
    {
        InitializeComponent();

        _panelA = new FingerprintPanel(PanelA, SampleComboA, ImageA, InfoA);
        _panelB = new FingerprintPanel(PanelB, SampleComboB, ImageB, InfoB);
        SampleComboA.ItemsSource = Samples;
        SampleComboB.ItemsSource = Samples;
        RejectedIcon.Source = Legend.RejectedIcon();

        _isReady = true;
        SampleComboA.SelectedIndex = 0; // 1_1 i 1_2 to ten sam palec
        SampleComboB.SelectedIndex = 1;
    }

    // przyciski i listy mają Tag="A" albo Tag="B", po nim wiadomo, którego odcisku dotyczą
    private FingerprintPanel PanelOf(object sender)
    {
        return (string)((FrameworkElement)sender).Tag == "B" ? _panelB : _panelA;
    }

    // ---------- wczytywanie obrazu ----------

    private void Sample_Changed(object sender, SelectionChangedEventArgs e)
    {
        FingerprintPanel panel = PanelOf(sender);
        if (_isReady && panel.Combo.SelectedItem is Sample sample)
        {
            var bitmap = new BitmapImage(new Uri("pack://application:,,,/Assets/" + sample.FileName));
            Load(panel, bitmap, sample.Name);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
        if (dialog.ShowDialog() == true)
        {
            LoadFile(PanelOf(sender), dialog.FileName);
        }
    }

    private void Panel_DragEnter(object sender, DragEventArgs e)
    {
        PanelOf(sender).Border.BorderBrush = Brushes.RoyalBlue; // podświetlenie miejsca upuszczenia
    }

    private void Panel_DragLeave(object sender, DragEventArgs e)
    {
        PanelOf(sender).Border.ClearValue(Border.BorderBrushProperty);
    }

    private void Panel_Drop(object sender, DragEventArgs e)
    {
        FingerprintPanel panel = PanelOf(sender);
        panel.Border.ClearValue(Border.BorderBrushProperty);
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
        {
            LoadFile(panel, files[0]);
        }
    }

    private void LoadFile(FingerprintPanel panel, string path)
    {
        try
        {
            var bitmap = new BitmapImage(new Uri(path));

            // własny plik nie jest żadną z próbek, więc czyścimy wybór na liście
            _isReady = false;
            panel.Combo.SelectedIndex = -1;
            _isReady = true;

            Load(panel, bitmap, Path.GetFileName(path));
        }
        catch (Exception)
        {
            StatusText.Text = $"Nie udało się wczytać {Path.GetFileName(path)}. Obsługiwane: PNG, JPG, BMP, TIFF.";
        }
    }

    private void Load(FingerprintPanel panel, BitmapSource bitmap, string name)
    {
        panel.Name = name;
        panel.Grayscale = ImageConversion.ToGrayscale(bitmap, out panel.Width, out panel.Height);
        Analyze(panel);
    }

    // ---------- analiza i wyświetlanie ----------

    private AnalysisOptions CurrentOptions()
    {
        return new AnalysisOptions(
            RemoveFalseMinutiae: RemoveFalseCheck.IsChecked == true,
            BorderMargin: (int)BorderSlider.Value,
            MinDistance: (int)DistanceSlider.Value);
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

    private void Analyze(FingerprintPanel panel)
    {
        if (panel.Grayscale == null)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        panel.Result = FingerprintAnalyzer.Analyze(panel.Grayscale, panel.Width, panel.Height, CurrentOptions());
        panel.Milliseconds = stopwatch.ElapsedMilliseconds;

        Draw(panel);
        UpdateTables();
    }

    private void Draw(FingerprintPanel panel)
    {
        if (panel.Result == null || panel.Grayscale == null)
        {
            return;
        }

        panel.Image.Source = MinutiaeRenderer.Render(
            panel.Result, panel.Grayscale, CurrentViewMode(), _visibleTypes, _visibleReasons);
        panel.Info.Text = $"{panel.Result.Minutiae.Count} minucji  ·  {panel.Name}  ·  {panel.Width}×{panel.Height} px";
    }

    // zmiana filtra -> trzeba policzyć minucje od nowa
    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_isReady)
        {
            Analyze(_panelA);
            Analyze(_panelB);
        }
    }

    private void Slider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BorderText != null && DistanceText != null)
        {
            BorderText.Text = $"{BorderSlider.Value} px";
            DistanceText.Text = $"{DistanceSlider.Value} px";
            Options_Changed(sender, e);
        }
    }

    // zmiana widoku -> wystarczy narysować jeszcze raz, bez ponownej analizy
    private void View_Changed(object sender, RoutedEventArgs e)
    {
        if (_isReady)
        {
            Draw(_panelA);
            Draw(_panelB);
        }
    }

    // checkbox w tabeli: pokaż / ukryj dany typ minucji albo daną przyczynę odrzucenia na obu obrazach
    private void Visibility_Changed(object sender, RoutedEventArgs e)
    {
        var checkBox = (CheckBox)sender;
        bool show = checkBox.IsChecked == true;
        bool changed = checkBox.Tag switch
        {
            MinutiaType type => show ? _visibleTypes.Add(type) : _visibleTypes.Remove(type),
            RejectionReason reason => show ? _visibleReasons.Add(reason) : _visibleReasons.Remove(reason),
            _ => false,
        };
        if (changed)
        {
            View_Changed(sender, e); // rysujemy od nowa tylko gdy coś się zmieniło
        }
    }

    // ---------- tabele po prawej ----------

    private void UpdateTables()
    {
        FingerprintAnalysis? a = _panelA.Result;
        FingerprintAnalysis? b = _panelB.Result;

        var counts = new List<TableRow>();
        foreach (MinutiaType type in Enum.GetValues<MinutiaType>())
        {
            counts.Add(new TableRow(Legend.MarkerIcon(type), Legend.NameOf(type), Legend.CrossingNumberOf(type),
                a?.Count(type), b?.Count(type), IsTotal: false, Legend.DescriptionOf(type))
            {
                Key = type,
                IsVisible = _visibleTypes.Contains(type),
            });
        }
        counts.Add(new TableRow(null, "Razem", "", a?.Minutiae.Count, b?.Minutiae.Count, IsTotal: true, null));
        CountsList.ItemsSource = counts;

        // gdy filtr jest wyłączony, liczby odrzuconych nie mają sensu - pokazujemy "—"
        bool filterOn = RemoveFalseCheck.IsChecked == true;
        var rejected = new List<TableRow>();
        foreach (RejectionReason reason in Enum.GetValues<RejectionReason>())
        {
            rejected.Add(new TableRow(Legend.RejectedIcon(), Legend.NameOf(reason), "",
                filterOn ? a?.Rejected.Count(r => r.Reason == reason) : null,
                filterOn ? b?.Rejected.Count(r => r.Reason == reason) : null,
                IsTotal: false, Legend.DescriptionOf(reason))
            {
                Key = reason,
                IsVisible = _visibleReasons.Contains(reason),
            });
        }
        rejected.Add(new TableRow(null, "Odrzucone razem", "",
            filterOn ? a?.Rejected.Count : null, filterOn ? b?.Rejected.Count : null, IsTotal: true, null));
        RejectedList.ItemsSource = rejected;

        StatusText.Text = $"Analiza: A {_panelA.Milliseconds} ms, B {_panelB.Milliseconds} ms. " +
                          "Przeciągnij obraz na panel A lub B, żeby go podmienić.";
    }

    // ---------- zapis ----------

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        FingerprintPanel panel = PanelOf(sender);
        if (panel.Image.Source is not BitmapSource image)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = Path.GetFileNameWithoutExtension(panel.Name).Replace(' ', '_') + "_minucje.png",
        };
        if (dialog.ShowDialog() == true)
        {
            ImageConversion.SavePng(image, dialog.FileName);
            StatusText.Text = "Zapisano: " + dialog.FileName;
        }
    }

    private sealed record Sample(string Name, string FileName);

    // wiersz tabeli; null w kolumnie A/B wyświetla się jako "—"
    public sealed record TableRow(ImageSource? Icon, string Name, string Detail, int? CountA, int? CountB,
        bool IsTotal, string? ToolTip)
    {
        public string A => CountA?.ToString() ?? "—";

        public string B => CountB?.ToString() ?? "—";

        // typ minucji albo przyczyna odrzucenia; tylko takie wiersze mają checkbox "pokaż na obrazie"
        public object? Key { get; init; }

        public bool IsVisible { get; init; }

        public bool HasCheckBox => Key != null;
    }

    // stan jednego z dwóch paneli: kontrolki + wczytany obraz + wynik analizy
    private sealed class FingerprintPanel
    {
        public FingerprintPanel(Border border, ComboBox combo, Image image, TextBlock info)
        {
            Border = border;
            Combo = combo;
            Image = image;
            Info = info;
        }

        public Border Border { get; }
        public ComboBox Combo { get; }
        public Image Image { get; }
        public TextBlock Info { get; }

        public string Name = "";
        public byte[]? Grayscale;
        public int Width;
        public int Height;
        public FingerprintAnalysis? Result;
        public long Milliseconds;
    }
}
