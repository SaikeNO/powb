using System.Diagnostics;
using System.IO;
using System.Text;
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
    private byte[]? _skeleton;
    private int _w, _h;          // rozmiar obrazu
    private int _W, _H;          // rozmiar z ramką
    private string _fileName = "";
    private bool _syncing, _loading;

    private ThinningAlgorithm Algorithm => (ThinningAlgorithm)AlgoCombo.SelectedIndex;

    public MainWindow()
    {
        InitializeComponent();
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
            _gray = ImageUtils.ToGray(bmp, out _w, out _h);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Nie udało się wczytać obrazu: " + ex.Message;
            return;
        }
        _W = _w + 2; _H = _h + 2;
        _fileName = Path.GetFileName(path);
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

    /// <summary>Binaryzacja + szkieletyzacja (jest szybka, więc działa na żywo).</summary>
    private void Rebuild()
    {
        if (_gray == null) return;
        _binary = ImageUtils.BinarizePadded(_gray, _w, _h, (int)ThresholdSlider.Value,
                                            DarkObjectCheck.IsChecked == true);
        BinaryImage.Source = ImageUtils.ToBitmap(_binary, _W, _H);
        Thin();
    }

    private void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _binary == null) return;
        Thin();
    }

    private void Thin()
    {
        if (_binary == null) return;
        var algo = Algorithm;
        var sw = Stopwatch.StartNew();
        _skeleton = Thinning.Run(algo, _binary, _W, _H);
        sw.Stop();

        ResultImage.Source = OverlayCheck.IsChecked == true
            ? ImageUtils.ToOverlay(_binary, _skeleton, _W, _H, Colors.Red)
            : ImageUtils.ToBitmap(_skeleton, _W, _H);
        ResultCaption.Text = $"Szkielet — {Thinning.Name(algo)}";
        SaveButton.IsEnabled = true;

        int obj = ImageUtils.CountObject(_binary), skel = ImageUtils.CountObject(_skeleton);
        StatusText.Text = $"{_fileName}  |  {_w}×{_h} px  |  {Thinning.Name(algo)}: {sw.Elapsed.TotalMilliseconds:F1} ms  |  " +
                          $"obiekt: {obj} px → szkielet: {skel} px" + (obj > 0 ? $" ({100.0 * skel / obj:F1}%)" : "");
    }

    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (_binary == null) { StatusText.Text = "Najpierw otwórz obraz."; return; }
        var sb = new StringBuilder($"{_fileName} {_w}×{_h}:  ");
        foreach (ThinningAlgorithm a in Enum.GetValues<ThinningAlgorithm>())
        {
            Thinning.Run(a, _binary, _W, _H);   // rozgrzewka JIT
            var sw = Stopwatch.StartNew();
            var res = Thinning.Run(a, _binary, _W, _H);
            sw.Stop();
            sb.Append($"{Thinning.Name(a)} {sw.Elapsed.TotalMilliseconds:F1} ms ({ImageUtils.CountObject(res)} px)   ");
        }
        StatusText.Text = sb.ToString();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_skeleton == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = $"{Path.GetFileNameWithoutExtension(_fileName)}_{Thinning.Name(Algorithm)}.png"
        };
        if (dlg.ShowDialog() != true) return;
        ImageUtils.SavePng(ImageUtils.ToBitmap(_skeleton, _W, _H), dlg.FileName);
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
