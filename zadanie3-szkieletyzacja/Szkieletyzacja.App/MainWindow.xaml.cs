using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
    private const double ZoomStep = 1.15;

    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    private static readonly SampleImage[] SampleImages =
    {
        new("Odcisk palca (próbka)", "fp_sample.png"),
        new("Odcisk palca 10_1", "fp_10_1.png"),
        new("Odcisk palca 1_1", "fp_1_1.png"),
        new("Odcisk palca 5_3", "fp_5_3.png"),
        new("Odcisk palca 12_2", "fp_12_2.png"),
        new("Kod QR", "qr.png"),
        new("Pismo odręczne 1", "handwritten.jpg"),
        new("Pismo odręczne 2", "handwritten2.jpg"),
        new("Tekst drukowany", "printed.jpg"),
        new("Jabłko", "apple.png"),
        new("Kot", "cat.png"),
    };

    private readonly ObservableCollection<AlgorithmRow> _algorithmRows = new();

    private byte[]? _grayscale;
    private byte[]? _binary;
    private int _imageWidth;
    private int _imageHeight;
    private int _paddedWidth;
    private int _paddedHeight;
    private string _imageName = "";
    private bool _isSynchronizingScroll;
    private bool _isSettingThreshold;
    private bool _isThinningRunning;

    public MainWindow()
    {
        InitializeComponent();

        foreach (ThinningAlgorithm algorithm in Enum.GetValues<ThinningAlgorithm>())
        {
            _algorithmRows.Add(new AlgorithmRow(algorithm));
        }
        AlgoList.ItemsSource = _algorithmRows;

        WarmUpAlgorithms();

        SampleCombo.ItemsSource = SampleImages;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        SampleCombo.SelectedIndex = 0;
    }

    private static void WarmUpAlgorithms()
    {
        const int imageSize = 40;
        const int paddedSize = imageSize + 2;
        var squareImage = new byte[paddedSize * paddedSize];
        for (int y = 5; y < 30; y++)
        {
            for (int x = 5; x < 30; x++)
            {
                squareImage[(y + 1) * paddedSize + x + 1] = 1;
            }
        }

        foreach (ThinningAlgorithm algorithm in Enum.GetValues<ThinningAlgorithm>())
        {
            Thinning.Run(algorithm, squareImage, paddedSize, paddedSize);
        }
    }

    private void Sample_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (SampleCombo.SelectedItem is not SampleImage sample)
        {
            return;
        }

        var bitmap = new BitmapImage(new Uri("pack://application:,,,/Assets/" + sample.FileName));
        bitmap.Freeze();
        LoadImage(bitmap, sample.Name);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
        if (dialog.ShowDialog() == true)
        {
            LoadImageFromFile(dialog.FileName);
        }
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            LoadImageFromFile(files[0]);
        }
    }

    private void LoadImageFromFile(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (Array.IndexOf(SupportedExtensions, extension) < 0)
        {
            StatusText.Text = "Nieobsługiwany format pliku.";
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();

            SampleCombo.SelectedIndex = -1;
            LoadImage(bitmap, Path.GetFileName(path));
        }
        catch (Exception exception)
        {
            StatusText.Text = "Nie udało się wczytać obrazu: " + exception.Message;
        }
    }

    private void LoadImage(BitmapSource bitmap, string imageName)
    {
        _grayscale = ImageUtils.ToGrayscale(bitmap, out _imageWidth, out _imageHeight);
        _paddedWidth = _imageWidth + 2;
        _paddedHeight = _imageHeight + 2;
        _imageName = imageName;

        int otsuThreshold = ImageUtils.ComputeOtsuThreshold(_grayscale);
        _isSettingThreshold = true;
        ThresholdSlider.Value = otsuThreshold;
        ThresholdText.Text = otsuThreshold.ToString();
        _isSettingThreshold = false;

        Binarize();
        FitToView();
    }

    private void Threshold_Changed(object sender, RoutedEventArgs e)
    {
        if (ThresholdText == null || _isSettingThreshold)
        {
            return;
        }

        ThresholdText.Text = ((int)ThresholdSlider.Value).ToString();
        Binarize();
    }

    private void Otsu_Click(object sender, RoutedEventArgs e)
    {
        if (_grayscale == null)
        {
            return;
        }

        ThresholdSlider.Value = ImageUtils.ComputeOtsuThreshold(_grayscale);
    }

    private void Binarize()
    {
        if (_grayscale == null)
        {
            return;
        }

        _binary = ImageUtils.BinarizeWithPadding(
            _grayscale, _imageWidth, _imageHeight, (int)ThresholdSlider.Value, DarkObjectCheck.IsChecked == true);
        BinaryImage.Source = ImageUtils.ToBitmap(_binary, _paddedWidth, _paddedHeight);

        foreach (AlgorithmRow row in _algorithmRows)
        {
            row.Reset();
        }
        ResultImage.Source = null;
        ResultCaption.Text = "Szkielet";
        SaveButton.IsEnabled = false;
        StartButton.IsEnabled = !_isThinningRunning;

        int objectPixels = ImageUtils.CountObjectPixels(_binary);
        StatusText.Text = $"{_imageName}  |  {_imageWidth}×{_imageHeight} px  |  obiekt: {objectPixels} px";
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_binary == null || _isThinningRunning)
        {
            return;
        }

        List<AlgorithmRow> selectedRows = _algorithmRows.Where(row => row.IsSelected).ToList();
        if (selectedRows.Count == 0)
        {
            StatusText.Text = "Zaznacz co najmniej jeden algorytm.";
            return;
        }

        _isThinningRunning = true;
        StartButton.IsEnabled = false;
        byte[] binary = _binary;
        int paddedWidth = _paddedWidth;
        int paddedHeight = _paddedHeight;

        foreach (AlgorithmRow row in selectedRows)
        {
            row.Reset();
        }

        foreach (AlgorithmRow row in selectedRows)
        {
            row.Info = "liczę…";

            (byte[] skeleton, double elapsedMilliseconds) = await Task.Run(() =>
            {
                var stopwatch = Stopwatch.StartNew();
                byte[] result = Thinning.Run(row.Algorithm, binary, paddedWidth, paddedHeight);
                stopwatch.Stop();
                return (result, stopwatch.Elapsed.TotalMilliseconds);
            });

            row.Skeleton = skeleton;
            row.Milliseconds = elapsedMilliseconds;
            row.SkeletonPixels = ImageUtils.CountObjectPixels(skeleton);
            row.Info = $"{elapsedMilliseconds:F2} ms  |  szkielet: {row.SkeletonPixels} px";
            AlgoList.SelectedItem = row;
        }

        _isThinningRunning = false;
        StartButton.IsEnabled = true;

        IEnumerable<string> timings = selectedRows.Select(row => $"{row.Name}: {row.Milliseconds:F2} ms");
        StatusText.Text = $"{_imageName}  |  " + string.Join("   ", timings);
    }

    private void AlgoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowSelectedSkeleton();
    }

    private void Overlay_Click(object sender, RoutedEventArgs e)
    {
        ShowSelectedSkeleton();
    }

    private void ShowSelectedSkeleton()
    {
        if (_binary == null || AlgoList.SelectedItem is not AlgorithmRow { Skeleton: { } skeleton } row)
        {
            ResultImage.Source = null;
            SaveButton.IsEnabled = false;
            return;
        }

        if (OverlayCheck.IsChecked == true)
        {
            ResultImage.Source = ImageUtils.ToOverlay(_binary, skeleton, _paddedWidth, _paddedHeight, Colors.Red);
        }
        else
        {
            ResultImage.Source = ImageUtils.ToBitmap(skeleton, _paddedWidth, _paddedHeight);
        }
        ResultCaption.Text = $"Szkielet — {row.Name} ({row.Milliseconds:F2} ms)";
        SaveButton.IsEnabled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (AlgoList.SelectedItem is not AlgorithmRow { Skeleton: { } skeleton } row)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PNG|*.png",
            FileName = $"{Path.GetFileNameWithoutExtension(_imageName)}_{row.Name}.png"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        ImageUtils.SavePng(ImageUtils.ToBitmap(skeleton, _paddedWidth, _paddedHeight), dialog.FileName);
        StatusText.Text = "Zapisano: " + dialog.FileName;
    }

    private void Zoom_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZoomText == null)
        {
            return;
        }

        double zoom = ZoomSlider.Value;
        ZoomText.Text = $"{zoom * 100:F0}%";
        var scale = new ScaleTransform(zoom, zoom);
        BinaryImage.LayoutTransform = scale;
        ResultImage.LayoutTransform = scale;
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        FitToView();
    }

    private void Zoom100_Click(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = 1;
    }

    private void FitToView()
    {
        if (_grayscale == null)
        {
            return;
        }

        UpdateLayout();
        double zoomToFitWidth = (LeftScroll.ViewportWidth - 2) / _imageWidth;
        double zoomToFitHeight = (LeftScroll.ViewportHeight - 2) / _imageHeight;
        double zoom = Math.Min(zoomToFitWidth, zoomToFitHeight);
        ZoomSlider.Value = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
    }

    private void Scroll_Wheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        double zoom = e.Delta > 0 ? ZoomSlider.Value * ZoomStep : ZoomSlider.Value / ZoomStep;
        ZoomSlider.Value = Math.Clamp(zoom, ZoomSlider.Minimum, ZoomSlider.Maximum);
        e.Handled = true;
    }

    private void Scroll_Changed(object sender, ScrollChangedEventArgs e)
    {
        if (_isSynchronizingScroll)
        {
            return;
        }

        var scrolledViewer = (ScrollViewer)sender;
        ScrollViewer otherViewer = scrolledViewer == LeftScroll ? RightScroll : LeftScroll;

        _isSynchronizingScroll = true;
        otherViewer.ScrollToHorizontalOffset(scrolledViewer.HorizontalOffset);
        otherViewer.ScrollToVerticalOffset(scrolledViewer.VerticalOffset);
        _isSynchronizingScroll = false;
    }

    private sealed record SampleImage(string Name, string FileName);
}
