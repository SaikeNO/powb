using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Binarization.Core;
using Microsoft.Win32;
namespace Binarization.App;

public partial class MainWindow : Window
{
    private readonly WrapPanel actions = new(), parameters = new();
    private readonly ComboBox algorithms = new() { Width = 150, Margin = new Thickness(0, 0, 10, 0) };
    private readonly Dictionary<string, TextBox> fields = new();
    private readonly Dictionary<string, StackPanel> panels = new();
    private readonly Image original = new() { Stretch = Stretch.Uniform }, grayscale = new() { Stretch = Stretch.Uniform };
    private readonly ListBox results = new();
    private readonly TextBlock status = new() { Text = "Load PNG, JPEG or BMP to begin.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
    private Button run = null!, compare = null!, save = null!;
    private LoadedImage? source;
    private bool busy, outdated;
    private Algorithm Selected => algorithms.SelectedItem is Algorithm a ? a : Algorithm.Threshold;

    public MainWindow()
    {
        InitializeComponent();
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        Root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Add(new TextBlock { Text = "Image binarization", FontSize = 28, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 16) }, 0);
        Add(actions, 1); Add(parameters, 2); Add(status, 4);
        Button MakeButton(string text, RoutedEventHandler handler)
        {
            var button = new Button { Content = text, Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 8, 12) };
            button.Click += handler; actions.Children.Add(button); return button;
        }
        MakeButton("Load image", Load); actions.Children.Add(algorithms);
        run = MakeButton("Run", async (_, _) => await Process(false));
        compare = MakeButton("Compare all", async (_, _) => await Process(true));
        save = MakeButton("Save selected PNG", Save);
        foreach (var (name, value) in new[] { ("Threshold", "128"), ("Window", "25"), ("Niblack k", "-0.2"), ("Sauvola k", "0.5"), ("Sauvola R", "128"), ("Phansalkar k", "0.25"), ("Phansalkar R", "0.5"), ("p", "2"), ("q", "10") })
        {
            var panel = new StackPanel { Width = 105, Margin = new Thickness(0, 0, 10, 12) };
            panel.Children.Add(new TextBlock { Text = name });
            var box = new TextBox { Text = value, Padding = new Thickness(6) };
            box.TextChanged += (_, _) => { if (results.Items.Count > 0) { outdated = true; status.Text = "Parameters changed — results outdated. Run again to update."; } };
            panel.Children.Add(box); fields[name] = box; panels[name] = panel; parameters.Children.Add(panel);
        }
        algorithms.SelectionChanged += (_, _) => RefreshFields();
        algorithms.ItemsSource = Enum.GetValues<Algorithm>(); algorithms.SelectedIndex = 0;
        var previews = new Grid(); previews.ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) }); previews.ColumnDefinitions.Add(new() { Width = new GridLength(3, GridUnitType.Star) });
        var inputs = new Grid { Margin = new Thickness(0, 0, 12, 0) }; inputs.RowDefinitions.Add(new()); inputs.RowDefinitions.Add(new());
        var originalBox = Box("Original", original); var grayBox = Box("Grayscale", grayscale); Grid.SetRow(grayBox, 1); inputs.Children.Add(originalBox); inputs.Children.Add(grayBox);
        previews.Children.Add(inputs); var outputBox = Box("Results — select image to export", results); Grid.SetColumn(outputBox, 1); previews.Children.Add(outputBox); Add(previews, 3);
        results.SelectionChanged += (_, _) => UpdateButtons(); UpdateButtons();
    }
    private static GroupBox Box(string title, UIElement content) => new() { Header = title, Content = content, Background = Brushes.White, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 8) };
    private void Add(UIElement control, int row) { Grid.SetRow(control, row); Root.Children.Add(control); }
    private void RefreshFields()
    {
        foreach (var (name, panel) in panels) panel.Visibility = (name switch
        {
            "Threshold" => Selected == Algorithm.Threshold,
            "Window" => Selected is Algorithm.Niblack or Algorithm.Sauvola or Algorithm.Phansalkar,
            "Niblack k" => Selected == Algorithm.Niblack,
            "Sauvola k" or "Sauvola R" => Selected == Algorithm.Sauvola,
            _ => Selected == Algorithm.Phansalkar
        }) ? Visibility.Visible : Visibility.Collapsed;
    }
    private Parameters Read(Algorithm algorithm)
    {
        int Integer(string key) => int.TryParse(fields[key].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : throw new ArgumentException($"{key} must be an integer.");
        double Number(string key) => double.TryParse(fields[key].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && double.IsFinite(v) ? v : throw new ArgumentException($"{key} must be finite. Use a decimal point.");
        var p = new Parameters();
        p = algorithm switch
        {
            Algorithm.Threshold => p with { Threshold = Integer("Threshold") },
            Algorithm.Niblack => p with { WindowSize = Integer("Window"), NiblackK = Number("Niblack k") },
            Algorithm.Sauvola => p with { WindowSize = Integer("Window"), SauvolaK = Number("Sauvola k"), SauvolaR = Number("Sauvola R") },
            Algorithm.Phansalkar => p with { WindowSize = Integer("Window"), PhansalkarK = Number("Phansalkar k"), PhansalkarR = Number("Phansalkar R"), P = Number("p"), Q = Number("q") },
            _ => p
        };
        p.Validate(algorithm); return p;
    }
    private void Load(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            LoadImage(dialog.FileName);
        }
        catch (Exception ex) { Error("Cannot load image", ex); }
    }
    internal void LoadImage(string path)
    {
        var loaded = ImageFiles.Load(path); source = loaded; original.Source = loaded.Original; grayscale.Source = null;
        results.Items.Clear(); outdated = false; UpdateButtons(); status.Text = $"{Path.GetFileName(path)} — {source.Width} × {source.Height} pixels — click Run or Compare all to process.";
    }
    internal async Task Process(bool all)
    {
        if (source is null || busy) return;
        try
        {
            var jobs = (all ? Enum.GetValues<Algorithm>() : new[] { Selected }).Select(a => (Algorithm: a, Parameters: Read(a))).ToArray();
            var input = source; busy = true; actions.IsEnabled = parameters.IsEnabled = false; status.Text = "Processing…";
            var processed = await Task.Run(() =>
            {
                var gray = GrayscaleConverter.ConvertBgra32(input.Width, input.Height, input.Bgra).Gray;
                var completed = jobs.Select(j => (j.Algorithm, Result: Binarizer.Run(gray, j.Algorithm, j.Parameters))).ToArray();
                return (Gray: gray, Results: completed);
            });
            grayscale.Source = ImageFiles.Preview(processed.Gray);
            var done = processed.Results;
            results.Items.Clear();
            foreach (var item in done)
            {
                var panel = new StackPanel();
                panel.Children.Add(new TextBlock { Text = $"{item.Algorithm} — {item.Result.Elapsed.TotalMilliseconds:F2} ms" + (item.Result.GlobalThreshold is int t ? $" — T = {t}" : ""), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
                panel.Children.Add(new Image { Source = ImageFiles.Preview(item.Result.Image), Height = 240, Stretch = Stretch.Uniform });
                results.Items.Add(new ListBoxItem { Content = panel, Tag = (item.Algorithm, item.Result), HorizontalContentAlignment = HorizontalAlignment.Stretch });
            }
            results.SelectedIndex = 0; outdated = false; status.Text = $"{done.Length} result(s) ready. Export preserves original resolution.";
        }
        catch (Exception ex) { Error("Cannot process image", ex); }
        finally { busy = false; actions.IsEnabled = parameters.IsEnabled = true; UpdateButtons(); }
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (results.SelectedItem is not ListBoxItem item || item.Tag is not ValueTuple<Algorithm, BinarizationResult> data) return;
        var dialog = new SaveFileDialog { Filter = "PNG image|*.png", DefaultExt = ".png", FileName = $"{data.Item1}.png" };
        if (dialog.ShowDialog(this) != true) return;
        try { ImageFiles.Save(data.Item2.Image, dialog.FileName); status.Text = $"Saved {dialog.FileName}" + (outdated ? " — result uses earlier parameters." : ""); }
        catch (Exception ex) { Error("Cannot save image", ex); }
    }
    private void UpdateButtons() { run.IsEnabled = compare.IsEnabled = source is not null && !busy; save.IsEnabled = results.SelectedItem is not null && !busy; }
    private void Error(string title, Exception ex) { status.Text = $"{title}: {ex.Message}"; MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning); }
}
