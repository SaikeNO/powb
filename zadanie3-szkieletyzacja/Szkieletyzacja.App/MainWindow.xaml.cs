using System.Diagnostics;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Szkieletyzacja.Core;

namespace Szkieletyzacja;

public partial class MainWindow : Window
{
    private static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    private byte[]? _gray;
    private byte[]? _binary;
    private int _w, _h;          // rozmiar obrazu
    private int _W, _H;          // rozmiar z ramką
    private string _fileName = "";
    private bool _syncing, _loading, _running;

    private readonly ObservableCollection<AlgoRow> _rows = new();

    public MainWindow()
    {
        InitializeComponent();
        foreach (ThinningAlgorithm a in Enum.GetValues<ThinningAlgorithm>())
            _rows.Add(new AlgoRow(a));
        AlgoList.ItemsSource = _rows;
        WarmUp();
        Loaded += (_, _) => LoadImage(new BitmapImage(new Uri("pack://application:,,,/Assets/sample.png")), "przykład: odcisk palca");
    }

    /// <summary>Wstępne uruchomienie na małym obrazie, żeby JIT nie zafałszował pomiarów.</summary>
    private static void WarmUp()
    {
        const int n = 40;
        var img = new byte[(n + 2) * (n + 2)];
        for (int y = 5; y < 30; y++)
            for (int x = 5; x < 30; x++) img[(y + 1) * (n + 2) + x + 1] = 1;
        foreach (ThinningAlgorithm a in Enum.GetValues<ThinningAlgorithm>())
            Thinning.Run(a, img, n + 2, n + 2);
    }

    // ---------- ładowanie ----------

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
        if (dlg.ShowDialog() == true) LoadImage(dlg.FileName);
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            LoadImage(files[0]);
    }

    private void LoadImage(string path)
    {
        if (Array.IndexOf(Extensions, Path.GetExtension(path).ToLowerInvariant()) < 0)
        {
            StatusText.Text = "Nieobsługiwany format pliku.";
            return;
        }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;   // nie blokuje pliku
            bmp.UriSource = new Uri(path);
            bmp.EndInit();
            bmp.Freeze();
            LoadImage(bmp, Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            StatusText.Text = "Nie udało się wczytać obrazu: " + ex.Message;
        }
    }

    private void LoadImage(BitmapSource bmp, string name)
    {
        _gray = ImageUtils.ToGray(bmp, out _w, out _h);
        _W = _w + 2; _H = _h + 2;
        _fileName = name;
        DropHint.Visibility = Visibility.Collapsed;

        // domyślny próg automatyczny (Otsu)
        int t = ImageUtils.Otsu(_gray);
        _loading = true;
        ThresholdSlider.Value = t;
        ThresholdText.Text = t.ToString();
        _loading = false;
        Rebuild();
        FitToView();
    }

    // ---------- binaryzacja ----------

    private void Threshold_Changed(object sender, RoutedEventArgs e)
    {
        if (ThresholdText == null || _loading) return;
        ThresholdText.Text = ((int)ThresholdSlider.Value).ToString();
        Rebuild();
    }

    private void Otsu_Click(object sender, RoutedEventArgs e)
    {
        if (_gray == null) return;
        ThresholdSlider.Value = ImageUtils.Otsu(_gray);
    }

    private void Rebuild()
    {
        if (_gray == null) return;
        _binary = ImageUtils.BinarizePadded(_gray, _w, _h, (int)ThresholdSlider.Value,
                                            DarkObjectCheck.IsChecked == true);
        BinaryImage.Source = ImageUtils.ToBitmap(_binary, _W, _H);
        foreach (var r in _rows) r.Reset();
        ResultImage.Source = null;
        ResultCaption.Text = "Szkielet";
        SaveButton.IsEnabled = false;
        StartButton.IsEnabled = !_running;
        int obj = ImageUtils.CountObject(_binary);
        StatusText.Text = $"{_fileName}  |  {_w}×{_h} px  |  obiekt: {obj} px  |  zaznacz algorytmy i kliknij Start.";
    }

    // ---------- szkieletyzacja ----------

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_binary == null || _running) return;
        var chosen = _rows.Where(r => r.Selected).ToList();
        if (chosen.Count == 0) { StatusText.Text = "Zaznacz co najmniej jeden algorytm."; return; }

        _running = true;
        StartButton.IsEnabled = false;
        var binary = _binary;
        int W = _W, H = _H;
        foreach (var r in chosen) r.Reset();

        foreach (var row in chosen)
        {
            row.Info = "liczę…";
            // wykonanie poza wątkiem UI; mierzony jest wyłącznie czas samego algorytmu
            var (result, ms) = await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var res = Thinning.Run(row.Algorithm, binary, W, H);
                sw.Stop();
                return (res, sw.Elapsed.TotalMilliseconds);
            });
            row.Result = result;
            row.Milliseconds = ms;
            row.Pixels = ImageUtils.CountObject(result);
            row.Info = $"{ms:F2} ms  |  szkielet: {row.Pixels} px";
            AlgoList.SelectedItem = row;   // pokazuje wynik bieżącego algorytmu
        }

        _running = false;
        StartButton.IsEnabled = true;
        StatusText.Text = $"{_fileName}  |  " + string.Join("   ", chosen.Select(r => $"{r.Name}: {r.Milliseconds:F2} ms"));
    }

    private void AlgoList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowSelected();

    private void Overlay_Click(object sender, RoutedEventArgs e) => ShowSelected();

    private void ShowSelected()
    {
        if (_binary == null || AlgoList.SelectedItem is not AlgoRow { Result: { } result } row)
        {
            ResultImage.Source = null;
            SaveButton.IsEnabled = false;
            return;
        }
        ResultImage.Source = OverlayCheck.IsChecked == true
            ? ImageUtils.ToOverlay(_binary, result, _W, _H, Colors.Red)
            : ImageUtils.ToBitmap(result, _W, _H);
        ResultCaption.Text = $"Szkielet — {row.Name} ({row.Milliseconds:F2} ms)";
        SaveButton.IsEnabled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (AlgoList.SelectedItem is not AlgoRow { Result: { } result } row) return;
        var dlg = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = $"{Path.GetFileNameWithoutExtension(_fileName)}_{row.Name}.png"
        };
        if (dlg.ShowDialog() != true) return;
        ImageUtils.SavePng(ImageUtils.ToBitmap(result, _W, _H), dlg.FileName);
        StatusText.Text = "Zapisano: " + dlg.FileName;
    }

    // ---------- zoom i przewijanie ----------

    private void Zoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomText == null) return;
        double z = ZoomSlider.Value;
        ZoomText.Text = $"{z * 100:F0}%";
        var t = new ScaleTransform(z, z);
        BinaryImage.LayoutTransform = t;
        ResultImage.LayoutTransform = t;
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => FitToView();

    private void Zoom100_Click(object sender, RoutedEventArgs e) => ZoomSlider.Value = 1;

    private void FitToView()
    {
        if (_gray == null) return;
        UpdateLayout();
        double zx = (LeftScroll.ViewportWidth - 2) / _w, zy = (LeftScroll.ViewportHeight - 2) / _h;
        ZoomSlider.Value = Math.Clamp(Math.Min(zx, zy), ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    private void Scroll_Wheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value * (e.Delta > 0 ? 1.15 : 1 / 1.15), ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    private void Scroll_Changed(object sender, ScrollChangedEventArgs e)
    {
        if (_syncing) return;
        var from = (ScrollViewer)sender;
        var to = from == LeftScroll ? RightScroll : LeftScroll;
        _syncing = true;
        to.ScrollToHorizontalOffset(from.HorizontalOffset);
        to.ScrollToVerticalOffset(from.VerticalOffset);
        _syncing = false;
    }
}

public sealed class AlgoRow : INotifyPropertyChanged
{
    private string _info = "—";
    private bool _selected = true;

    public AlgoRow(ThinningAlgorithm algorithm) { Algorithm = algorithm; }

    public ThinningAlgorithm Algorithm { get; }
    public string Name => Thinning.Name(Algorithm);
    public byte[]? Result { get; set; }
    public double Milliseconds { get; set; }
    public int Pixels { get; set; }

    public bool Selected { get => _selected; set { _selected = value; Raise(nameof(Selected)); } }
    public string Info { get => _info; set { _info = value; Raise(nameof(Info)); } }

    public void Reset() { Result = null; Milliseconds = 0; Pixels = 0; Info = "—"; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
