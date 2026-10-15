using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Binarization.Core;
using Microsoft.Win32;

namespace Binarization.App;

public partial class MainWindow : Window
{
    private double[]? _gray;
    private int _width, _height;

    public MainWindow() => InitializeComponent();

    private void Window_Loaded(object sender, RoutedEventArgs e) => LoadSample();
    private void Sample_Click(object sender, RoutedEventArgs e) => LoadSample();
    private void Settings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_gray is not null) Recalculate();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Otwórz obraz",
            Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.wdp|Wszystkie pliki|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var stream = File.OpenRead(dialog.FileName);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            SetImage(decoder.Frames[0]);
            FileLabel.Text = Path.GetFileName(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException
            or FileFormatException or ArgumentException or System.Runtime.InteropServices.COMException)
        {
            MessageBox.Show(this, $"Nie można wczytać obrazu.\n{ex.Message}", "Błąd obrazu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetImage(BitmapSource source)
    {
        double scale = Math.Min(1, 700.0 / Math.Max(source.PixelWidth, source.PixelHeight));
        int width = Math.Max(1, (int)Math.Floor(source.PixelWidth * scale + 0.5));
        int height = Math.Max(1, (int)Math.Floor(source.PixelHeight * scale + 0.5));
        // Drawing into a 96 DPI target also makes image dimensions independent of source DPI.
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
            drawing.DrawImage(source, new Rect(0, 0, width, height));
        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var converted = new FormatConvertedBitmap(target, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(width * height * 4)];
        converted.CopyPixels(pixels, width * 4, 0);
        _gray = Binarizer.ToGrayscale(pixels);
        _width = width;
        _height = height;
        OriginalPreview.Source = ToBitmap(_gray.Select(v => (byte)Math.Round(v, MidpointRounding.ToEven)).ToArray(), width, height);
        Recalculate();
    }

    private void Recalculate()
    {
        var settings = new BinarizationSettings((int)ThresholdSlider.Value, (int)WindowSlider.Value,
            NiblackSlider.Value, SauvolaSlider.Value, PhansalkarSlider.Value);
        var result = Binarizer.Process(_gray!, _width, _height, settings);
        ThresholdPreview.Source = ToBitmap(result.Threshold, _width, _height);
        NiblackPreview.Source = ToBitmap(result.Niblack, _width, _height);
        SauvolaPreview.Source = ToBitmap(result.Sauvola, _width, _height);
        PhansalkarPreview.Source = ToBitmap(result.Phansalkar, _width, _height);
        OtsuPreview.Source = ToBitmap(result.Otsu, _width, _height);
        ThresholdCaption.Text = $"Progowa (T = {settings.Threshold})";
        OtsuCaption.Text = $"Otsu (T = {result.OtsuThreshold})";
        StatusText.Text = $"{_width} × {_height} px • Gotowe • Zmień parametry, aby porównać metody.";
    }

    private static BitmapSource ToBitmap(byte[] pixels, int width, int height)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
        bitmap.Freeze();
        return bitmap;
    }

    private void LoadSample()
    {
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new LinearGradientBrush(Color.FromRgb(42, 42, 42), Color.FromRgb(232, 232, 232), 0),
                null, new Rect(0, 0, 600, 360));
            var ink = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0));
            void Text(string value, string font, double size, double x, double y, bool bold = false)
            {
                var typeface = new Typeface(new FontFamily(font), FontStyles.Normal,
                    bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
                drawing.DrawText(new FormattedText(value, CultureInfo.GetCultureInfo("pl-PL"),
                    FlowDirection.LeftToRight, typeface, size, ink, 1), new Point(x, y));
            }
            Text("Binaryzacja", "Arial", 64, 40, 52, true);
            Text("Niblack, Sauvola, Phansalkar, Otsu", "Times New Roman", 28, 40, 158);
            Text("Nierównomierne oświetlenie tła pokazuje", "Consolas", 20, 40, 228);
            Text("przewagę metod lokalnych nad globalnymi.", "Consolas", 20, 40, 258);
        }
        var target = new RenderTargetBitmap(600, 360, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var pixels = new byte[600 * 360 * 4];
        target.CopyPixels(pixels, 600 * 4, 0);
        for (int i = 0; i < pixels.Length; i += 4)
        {
            double noise = (Random.Shared.NextDouble() - 0.5) * 24;
            for (int channel = 0; channel < 3; channel++)
                pixels[i + channel] = (byte)Math.Clamp(Math.Round(pixels[i + channel] + noise, MidpointRounding.ToEven), 0, 255);
        }
        SetImage(BitmapSource.Create(600, 360, 96, 96, PixelFormats.Bgra32, null, pixels, 600 * 4));
        FileLabel.Text = "Obraz testowy";
    }

}
