using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Binarization.App;
using Binarization.Core;
namespace Binarization.Tests;

public class ImageFileTests
{
    [Fact]
    public void CodecsTransparencyAndBinaryExport()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "Binarization-codec-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            try
            {
                // Transparent black, opaque red, opaque white, opaque black.
                byte[] pixels = [0, 0, 0, 0, 0, 0, 255, 255, 255, 255, 255, 255, 0, 0, 0, 255];
                var bitmap = BitmapSource.Create(4, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 16);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
                string path = Path.Combine(directory, "alpha.png");
                using (var file = File.Create(path)) png.Save(file);
                var loaded = ImageFiles.Load(path);
                Assert.Equal(new byte[] { 255, 76, 255, 0 }, loaded.Gray.Pixels.ToArray());
                var binary = Binarizer.Run(loaded.Gray, Algorithm.Threshold).Image;
                string output = Path.Combine(directory, "binary.png"); ImageFiles.Save(binary, output);
                var reloaded = ImageFiles.Load(output).Gray;
                Assert.Equal(4, reloaded.Width); Assert.Equal(1, reloaded.Height);
                Assert.Equal(binary.Pixels.ToArray(), reloaded.Pixels.ToArray());
                foreach (BitmapEncoder encoder in new BitmapEncoder[] { new JpegBitmapEncoder(), new BmpBitmapEncoder() })
                {
                    encoder.Frames.Add(BitmapFrame.Create(loaded.Original));
                    string encoded = Path.Combine(directory, encoder is JpegBitmapEncoder ? "test.jpg" : "test.bmp");
                    using (var file = File.Create(encoded)) encoder.Save(file);
                    var decoded = ImageFiles.Load(encoded).Gray;
                    Assert.Equal(4, decoded.Width); Assert.Equal(1, decoded.Height);
                    foreach (Algorithm algorithm in Enum.GetValues<Algorithm>())
                        Assert.All(Binarizer.Run(decoded, algorithm).Image.Pixels.ToArray(), v => Assert.True(v is 0 or 255));
                }
                Assert.ThrowsAny<Exception>(() => ImageFiles.Load(Path.Combine(directory, "missing.png")));
                File.WriteAllText(Path.Combine(directory, "broken.png"), "not an image");
                Assert.ThrowsAny<Exception>(() => ImageFiles.Load(Path.Combine(directory, "broken.png")));
            }
            catch (Exception ex) { failure = ex; }
            finally { foreach (string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
