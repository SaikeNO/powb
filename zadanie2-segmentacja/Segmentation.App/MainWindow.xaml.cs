using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Segmentation.Core;

namespace Segmentation.App;

public partial class MainWindow : Window
{
    private byte[]? source;
    private byte[]? selection;
    private int width, height;
    private string fileName = "";

    public MainWindow() => InitializeComponent();

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Otwórz obraz",
            Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|Wszystkie pliki|*.*"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var stream = File.OpenRead(dialog.FileName);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            BitmapSource bitmap = decoder.Frames[0];
            double scale = Math.Min(1, 640.0 / bitmap.PixelWidth);
            int newWidth = Math.Max(1, (int)Math.Floor(bitmap.PixelWidth * scale + 0.5));
            int newHeight = Math.Max(1, (int)Math.Floor(bitmap.PixelHeight * scale + 0.5));
            // Render at 96 DPI to make displayed and selected pixel coordinates identical.
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
                drawing.DrawImage(bitmap, new Rect(0, 0, newWidth, newHeight));
            var resized = new RenderTargetBitmap(newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
            resized.Render(visual);
            var converted = new FormatConvertedBitmap(resized, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[checked(newWidth * newHeight * 4)];
            converted.CopyPixels(pixels, checked(newWidth * 4), 0);

            width = newWidth;
            height = newHeight;
            source = pixels;
            selection = new byte[width * height];
            fileName = Path.GetFileName(dialog.FileName);
            MorphControls.IsEnabled = true;
            RenderSelection();
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException
            or System.Runtime.InteropServices.COMException or OverflowException)
        {
            MessageBox.Show(this, $"Nie można otworzyć obrazu.\n{ex.Message}", "Błąd",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Image_Click(object sender, MouseButtonEventArgs e)
    {
        if (source is null) return;
        if (!int.TryParse(MaxPixelsInput.Text, out int limit) || limit < 0)
        {
            MessageBox.Show(this, "Podaj całkowitą liczbę pikseli od 0 do 2147483647.",
                "Nieprawidłowy limit", MessageBoxButton.OK, MessageBoxImage.Warning);
            MaxPixelsInput.Focus();
            MaxPixelsInput.SelectAll();
            return;
        }
        Point point = e.GetPosition(Preview);
        int x = (int)Math.Floor(point.X), y = (int)Math.Floor(point.Y);
        if (x < 0 || y < 0 || x >= width || y >= height) return;
        selection = MagicWand.Select(source, width, height, x, y,
            (int)ToleranceSlider.Value, limit, GlobalCheck.IsChecked == true);
        RenderSelection();
    }

    private void Erode_Click(object sender, RoutedEventArgs e) => Morph(false);
    private void Dilate_Click(object sender, RoutedEventArgs e) => Morph(true);

    private void Morph(bool dilate)
    {
        if (selection is null) return;
        selection = MagicWand.Morph(selection, width, height, dilate);
        RenderSelection();
    }

    private void RenderSelection()
    {
        if (source is null || selection is null) return;
        var pixels = (byte[])source.Clone();
        int count = 0;
        for (int i = 0; i < selection.Length; i++)
        {
            int offset = i * 4;
            if (selection[i] != 0)
            {
                pixels[offset] = (byte)Math.Round(source[offset] * 0.4, MidpointRounding.ToEven);
                pixels[offset + 1] = (byte)Math.Round(source[offset + 1] * 0.4, MidpointRounding.ToEven);
                pixels[offset + 2] = 255;
                count++;
            }
            pixels[offset + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32,
            null, pixels, width * 4);
        bitmap.Freeze();
        Preview.Source = bitmap;
        StatusText.Text = $"{fileName} • {width} × {height} • Zaznaczone piksele: {count}";
    }
}

