# Binarization

Native Windows WPF application in C# / .NET 10 for image binarization. The interface is in Polish. Processing runs locally in C#.

## Build and run

Requires Windows and .NET 10 SDK. From this directory:

```powershell
dotnet build Binarization.slnx
dotnet run --project Binarization.App
dotnet test Binarization.slnx
```

The app and core library have no external package dependencies. Tests use xUnit and require NuGet on first restore.

## Usage

The app starts with a generated 600 × 360 test image containing text, noise and uneven lighting. Choose **Otwórz obraz…** to load PNG, JPEG, BMP, GIF, TIFF, ICO or WDP; the first frame is used. Images are proportionally reduced to at most 700 pixels on the longest side, without enlarging smaller images. **Obraz testowy** generates another sample.

Six previews show grayscale input and global threshold, Niblack, Sauvola, Phansalkar and Otsu outputs. Slider defaults and ranges: threshold 128 (0–255), odd window 25 (3–101), Niblack k −0.20 (−1–0.5), Sauvola k 0.50 (0–1), Phansalkar k 0.25 (0–1). Results recalculate directly on each slider change, on the UI thread. Both global methods show their thresholds.

## Algorithm details

Grayscale uses `0.299 R + 0.587 G + 0.114 B`, ignoring alpha and retaining fractional values during processing. Local population statistics use summed-area tables and windows clipped at image edges. White pixels require a strict `value > threshold` comparison. Sauvola uses R = 128. Phansalkar normalizes luminance and statistics to [0, 1], with p = 2, q = 10 and R = 0.5. Otsu bins fractional values by truncation, selects the first threshold on ties and retains threshold zero for constant images.

`Binarization.App` contains WPF UI, decoding and sample generation; `Binarization.Core` contains algorithms; `Binarization.Tests` checks RGB conversion, fractional thresholds, Otsu ties and constant inputs, border statistics against direct neighborhood calculations, validation and cancellation.
