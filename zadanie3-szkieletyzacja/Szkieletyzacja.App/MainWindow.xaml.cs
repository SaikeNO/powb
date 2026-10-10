using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Szkieletyzacja.Core;

namespace Szkieletyzacja;

public partial class MainWindow : Window
{
    private byte[]? _gray;
    private byte[]? _binary;
    private int _w, _h;          // rozmiar obrazu
    private int _W, _H;          // rozmiar z ramką

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Obrazy|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
        if (dlg.ShowDialog() != true) return;

        var bmp = new BitmapImage(new System.Uri(dlg.FileName));
        _gray = ImageUtils.ToGray(bmp, out _w, out _h);
        _W = _w + 2; _H = _h + 2;
        Binarize();
    }

    private void Threshold_Changed(object sender, RoutedEventArgs e)
    {
        if (ThresholdText == null) return;
        ThresholdText.Text = ((int)ThresholdSlider.Value).ToString();
        Binarize();
    }

    private void Binarize()
    {
        if (_gray == null) return;
        _binary = ImageUtils.BinarizePadded(_gray, _w, _h, (int)ThresholdSlider.Value,
                                            DarkObjectCheck.IsChecked == true);
        BinaryImage.Source = ImageUtils.ToBitmap(_binary, _W, _H);
        ResultImage.Source = null;
        TimeText.Text = "";
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_binary == null) { MessageBox.Show("Najpierw otwórz obraz."); return; }

        var sw = Stopwatch.StartNew();
        byte[] result = AlgoCombo.SelectedIndex == 0
            ? Thinning.ZhangSuen(_binary, _W, _H)
            : Thinning.Kmm(_binary, _W, _H);
        sw.Stop();

        ResultImage.Source = ImageUtils.ToBitmap(result, _W, _H);
        TimeText.Text = $"Czas: {sw.ElapsedMilliseconds} ms";
    }
}
