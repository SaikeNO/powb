# Skeletonization (thinning) — Image Processing in Biometrics

C# / .NET 10 Windows desktop assignment. Two thinning algorithms, **Zhang-Suen** and **KMM**, are implemented from scratch; WPF is used only for the UI and image decoding.

## Build and run

Requirements: Windows and [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). NuGet access is needed for the xUnit test packages on first restore.

```powershell
dotnet build Szkieletyzacja.slnx
dotnet run --project Szkieletyzacja.App
dotnet test Szkieletyzacja.slnx
```

Projects: `Szkieletyzacja.Core` (`net10.0`) contains the algorithms, `Szkieletyzacja.App` (`net10.0-windows`) contains the WPF UI and image conversion, `Szkieletyzacja.Tests` contains xUnit tests of the algorithms.

## Demo

1. Click **Open image** and choose PNG, JPEG, BMP or TIFF. The image is converted to grayscale and binarized with a simple threshold.
2. Adjust the **threshold** slider and the **dark object** checkbox until the shape you want to thin is black in the left preview.
3. Choose **Zhang-Suen** or **KMM** and click **Thin**. The skeleton appears on the right together with the elapsed time.

Good test images: fingerprints, clean drawings, QR codes. Photos without clear shapes produce noise rather than a useful skeleton.

## Algorithms

Input is a 0/1 array (1 = object) with a 1-pixel background border, so neighbours are read without bounds checks. Both algorithms repeatedly peel border pixels until nothing changes. A pixel may be removed only if it is not a line end, not interior, and its removal does not disconnect the shape.

For pixel `P1` with neighbours `P2..P9` (clockwise from the top):

- `B` = number of object neighbours. Removal requires a border pixel that is not an end point.
- `A` = number of 0→1 transitions around `P2,P3,…,P9,P2`. `A = 1` means the object neighbours form one connected group, so removal keeps the shape connected.

### Zhang-Suen

Two sub-passes per iteration; candidates are collected first and deleted after the pass (parallel deletion).

| Sub-pass | Conditions |
|---|---|
| 1 | `2 ≤ B ≤ 6`, `A = 1`, `P2·P4·P6 = 0`, `P4·P6·P8 = 0` |
| 2 | `2 ≤ B ≤ 6`, `A = 1`, `P2·P4·P8 = 0`, `P2·P6·P8 = 0` |

### KMM (variant)

Each neighbour has a weight (`N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128`), and the sum of weights of object neighbours indexes a 256-entry `Deletable` table. Every iteration has two phases: pixels with a background 4-neighbour first, then corner pixels. Pixels are deleted sequentially, evaluated on the current image.

**Note:** the `Deletable` table is generated from the `B`/`A` rules (`2 ≤ B ≤ 7`, `A = 1`) rather than copied from the original KMM paper, so this is a KMM-style variant with the same weight mask and phase structure. Replace `BuildDeletableTable` in `Thinning.cs` if the exact original table is required.

### Speed

Algorithms work on flat `byte[]` arrays (no `GetPixel`/`SetPixel`). A test thins a random 1500×1500 image and asserts it finishes in under one second.

## Core API

```csharp
// image: flat 0/1 array with a 1-pixel border, so W = width + 2, H = height + 2
byte[] skeleton = Thinning.ZhangSuen(image, W, H);
byte[] skeleton2 = Thinning.Kmm(image, W, H);
```

Both methods return a new array and do not modify the input.

## Tests

xUnit tests run for both algorithms: empty image, single pixel, 1-pixel line unchanged, thick line becomes thin, connectivity of a thick plus sign, two objects stay two, ring keeps its hole, idempotence, input not modified, and a 1500×1500 speed check.

## References

- Zhang, Suen, *A fast parallel algorithm for thinning digital patterns*, Communications of the ACM, 1984.
- Saeed, Tabędzki, Rybnik, Adamski, *K3M: A universal algorithm for image skeletonization and a review of thinning techniques*, 2010 (also describes KMM).
