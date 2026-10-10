using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Binarization.App;
using Binarization.Core;
namespace Binarization.Tests;

public class WindowTests
{
    [Fact]
    public void WindowLoadsRunsAllMethodsAndMarksResultsOutdated()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                string path = Path.Combine(Path.GetTempPath(), $"Binarization-window-{Guid.NewGuid()}.png");
                MainWindow? window = null;
                try
                {
                    window = new MainWindow();
                    var controls = Descendants(window).ToArray();
                    var run = controls.OfType<Button>().Single(b => Equals(b.Content, "Run"));
                    var save = controls.OfType<Button>().Single(b => Equals(b.Content, "Save selected PNG"));
                    Assert.False(run.IsEnabled); Assert.False(save.IsEnabled);
                    ImageFiles.Save(new GrayImage(3, 2, [0, 50, 100, 150, 200, 255]), path);
                    window.LoadImage(path); Assert.True(run.IsEnabled);
                    var grayPreview = (Image)controls.OfType<GroupBox>().Single(b => Equals(b.Header, "Grayscale")).Content;
                    Assert.Null(grayPreview.Source);
                    await window.Process(true);
                    Assert.NotNull(grayPreview.Source);
                    var list = controls.OfType<ListBox>().Single();
                    Assert.Equal(5, list.Items.Count); Assert.True(save.IsEnabled);
                    Assert.All(list.Items.Cast<ListBoxItem>(), item =>
                    {
                        var (_, result) = ((Algorithm, BinarizationResult))item.Tag;
                        Assert.Equal(3, result.Image.Width); Assert.Equal(2, result.Image.Height);
                    });
                    var selector = controls.OfType<ComboBox>().Single();
                    foreach (Algorithm algorithm in Enum.GetValues<Algorithm>())
                    {
                        selector.SelectedItem = algorithm; await window.Process(false); Assert.Single(list.Items.Cast<object>());
                    }
                    controls.OfType<TextBox>().First().Text = "140";
                    Assert.Contains(controls.OfType<TextBlock>(), text => text.Text.Contains("outdated"));
                    window.LoadImage(path);
                    Assert.Null(grayPreview.Source);
                    Assert.Empty(list.Items.Cast<object>());
                    Assert.False(save.IsEnabled);
                }
                catch (Exception ex) { failure = ex; }
                finally { window?.Close(); if (File.Exists(path)) File.Delete(path); dispatcher.InvokeShutdown(); }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF workflow did not complete within 30 seconds.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
