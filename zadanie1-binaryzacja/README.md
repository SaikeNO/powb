# Binarization — Image Processing in Biometrics

C# / .NET 10 Windows desktop assignment. All five thresholding algorithms are implemented from scratch; WPF is used only for UI and image decoding/encoding. Grayscale conversion and alpha compositing belong to Core.

## Build and run

Requirements: Windows and [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). No additional workloads are required. NuGet access is needed for the xUnit test packages on first restore.

```powershell
dotnet build Binarization.slnx
dotnet run --project src/Binarization.App
dotnet test Binarization.slnx
```

Projects: `Binarization.Core` (`net10.0`) contains algorithms, `Binarization.App` (`net10.0-windows`) contains the WPF UI/codecs, and `Binarization.Tests` contains independent numerical checks.

## Demo

1. Click **Load image** and choose PNG, JPEG or BMP. The original color preview appears. Loading only decodes the file and retains its BGRA pixels; it performs no grayscale conversion.
2. Choose an algorithm, edit its visible parameters, and click **Run**. Processing converts the source to grayscale in Core, displays the grayscale preview, then shows the binary result. Use a decimal point for fractional values.
3. Repeat for local methods to tune their parameters. Values remain stored when changing algorithms.
4. Click **Compare all** to compute five labeled results with the current stored parameters. Shared window size applies to all local methods. Grayscale conversion runs once per processing request, shared by all five algorithms; it runs on the background task. Loading another image clears the grayscale preview and results.
5. Select a result and click **Save selected PNG**. Export is an 8-bit grayscale PNG at the original pixel dimensions with only 0 and 255 values.

Results show elapsed algorithm time and the global threshold for Threshold/Otsu. Processing runs on a background task; load/run/save/parameter controls are disabled while it runs. Editing parameters marks existing results outdated; exporting them still saves the displayed result, with an outdated notice in the status bar. Preview scaling never changes processing resolution.

## Algorithms and assignment coverage

For every method, input intensity **strictly greater** than threshold becomes white (255); otherwise it becomes black (0). Thresholds are not rounded or clamped before comparison.

| Method | Assignment | Formula | Defaults |
|---|---|---|---|
| Threshold | 20% | User-selected `T` | `T = 128` |
| Niblack | Shared 40% | `T = m + k*s` | `k = -0.2` |
| Sauvola | Shared 40% | `T = m*(1 + k*(s/R - 1))` | `k = 0.5`, `R = 128` |
| Phansalkar | Shared 40% | `T = m*(1 + p*exp(-q*m) + k*(s/R - 1))` | `k = 0.25`, `R = 0.5`, `p = 2`, `q = 10` |
| Otsu | 40% | Maximize weighted between-class variance | Automatic global threshold |

`m` and `s` denote local mean and population standard deviation. Niblack/Sauvola use intensities 0–255. Phansalkar uses intensities divided by 255, so its mean, standard deviation and threshold use the 0–1 scale; its threshold is multiplied by 255 before comparing to input bytes. This normalization is essential for the exponential term.

Local methods use square windows, default **25 × 25**. Window size must be a positive odd integer. At edges, clip the window to the image and divide by the actual number of included pixels; oversized windows are supported. Integral images of intensity and squared intensity give constant-time neighborhood statistics and O(width × height) processing. Compute `s = sqrt(max(0, E[I²] - m²))` to suppress negative floating-point variance caused by rounding. Two double integral buffers consume about 16 bytes per pixel in addition to input/output/preview buffers.

Otsu builds a 256-bin histogram. For each threshold `t`, classes are `[0,t]` and `[t+1,255]`; skip empty classes, and maximize `n0*n1*(mean0-mean1)²`. This omits a constant normalization factor without changing the optimum. Ties choose the lowest threshold. A constant image uses its sole intensity as threshold and consequently produces all black under the strict comparison rule. Complexity: O(pixel count + 256).

Image input is decoded through WPF codecs. Transparency is composited onto white before `round(0.299R + 0.587G + 0.114B)` grayscale conversion (C# midpoint-to-even rounding). Processing uses an 8-bit representation; for multi-frame input, only the first frame is used.

## Core API

`GrayscaleConverter.ConvertBgra32(width, height, bgra)` accepts a tightly packed BGRA32 buffer with straight (not premultiplied) alpha. It does not modify its input. It returns `GrayscaleConversion`, containing a `GrayImage` and opaque BGRA pixels for the original preview. Core handles white-background alpha compositing and luminance calculation; App handles only WPF codecs and bitmap presentation. The converter has no WPF or Windows dependency.

Each algorithm has its own class: `ThresholdBinarizer`, `NiblackBinarizer`, `SauvolaBinarizer`, `PhansalkarBinarizer`, and `OtsuBinarizer`. All inherit `BinarizationAlgorithm`, which handles validation and timing. Local methods share integral neighborhood calculations through `LocalBinarizer`; each concrete class owns its threshold formula. `Binarizer.Run` remains the enum-based entry point used by the GUI.

Classes can also run directly: `new SauvolaBinarizer().Run(image, parameters)`.

```csharp
var image = new GrayImage(width, height, grayscaleBytes);
var result = Binarizer.Run(image, Algorithm.Sauvola,
    new Parameters(WindowSize: 25, SauvolaK: 0.5, SauvolaR: 128));
byte[] binary = result.Image.Pixels.ToArray();
```

`GrayImage` copies its input buffer and checks dimensions. `BinarizationResult` includes output image, elapsed time, and nullable global threshold. Parameters are validated for the selected algorithm: threshold 0–255, odd positive window, finite values, positive R. Finite but extreme parameters that produce a nonfinite threshold are rejected. The UI reports invalid parameters and file errors without discarding previous results. No biometric recognition, report generation, or online submission is included.

## Verification

xUnit checks strict threshold equality, Otsu bimodal/tied/constant cases and an independent minimum within-class variance reference. Local algorithms are compared against a naive neighborhood implementation using centered variance, including border pixels, one-pixel windows, oversized windows, constant input and explicit Phansalkar normalization. Validation and buffer ownership are also tested. Windows-specific tests verify PNG/JPEG/BMP decoding, transparency, binary PNG round trips, and an in-process WPF workflow covering image loading, all algorithms, comparison, enabled buttons and outdated-result notices.

Manual smoke checklist: load PNG/JPEG/BMP, run each algorithm, compare all, select/export/reload results, reject invalid parameters, and check the outdated-result notice. Use a fingerprint scan or unevenly illuminated text image to demonstrate differences; no biometric dataset is bundled.

## References

- [ImageJ Auto Local Threshold](https://imagej.net/plugins/auto-local-threshold): local formulas and defaults. This app uses square rather than ImageJ's circular neighborhoods.
- Niblack, *An Introduction to Digital Image Processing*, 1986.
- [Sauvola and Pietikäinen, Adaptive document image binarization, 2000](https://doi.org/10.1016/S0031-3203(99)00055-2).
- [Phansalkar et al., Adaptive local thresholding for detection of nuclei in diversity stained cytology images, 2011](https://doi.org/10.1109/ICCSP.2011.5739305).
- [Otsu, A Threshold Selection Method from Gray-Level Histograms, 1979](https://doi.org/10.1109/TSMC.1979.4310076).
