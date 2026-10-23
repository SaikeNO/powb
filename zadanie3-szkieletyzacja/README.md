# Skeletonization (thinning) — Image Processing in Biometrics

C# / .NET 10 Windows desktop assignment. Four thinning algorithms, **Zhang-Suen**, **Guo-Hall**, **KMM** and **K3M**, are implemented from scratch; WPF is used only for the UI and image decoding.

## Build and run

Requirements: Windows and [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). NuGet access is needed for the xUnit test packages on first restore.

```powershell
dotnet build Szkieletyzacja.slnx
dotnet run --project Szkieletyzacja.App
dotnet test Szkieletyzacja.slnx
```

Projects: `Szkieletyzacja.Core` (`net10.0`) contains the algorithms, `Szkieletyzacja.App` (`net10.0-windows`) contains the WPF UI and image conversion, `Szkieletyzacja.Tests` contains xUnit tests of the algorithms.

## Demo

1. Open an image (button or drag & drop; PNG, JPEG, BMP, TIFF). It is binarized with the Otsu threshold automatically.
2. Adjust the **threshold** / **dark object** if needed. 
3. A sample fingerprint is loaded at start. Tick any algorithms and press **Start**: they run one after another and each row shows its real execution time (algorithm only, after a JIT warm-up) and skeleton size. Click a row to view its result, toggle the **overlay** (skeleton in red over the shape), zoom with the slider or Ctrl+wheel (both previews scroll together), **Save** writes the selected skeleton as PNG.

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

### KMM (Saeed, Rybnik, Tabędzki 2001)

Implemented as in the paper: (1) contour pixels touching the background by an edge become `2`, by a corner only `3`; (2) contour pixels whose weight is one of the 24 patterns with 2, 3 or 4 neighbours become `4` and all `4`s are deleted; (3) for `N = 2` and then `N = 3` every pixel labelled `N` is deleted if its neighbour weight is in the paper's 120-element *Deletion Array*, otherwise it returns to `1`. Repeated until nothing is deleted.

### Guo-Hall

Same two sub-pass structure as Zhang-Suen, with the conditions `C(P)=1`, `2 ≤ min(N1,N2) ≤ 3` and the sub-pass tests `(P2∨P3∨¬P5)∧P4=0` / `(P6∨P7∨¬P9)∧P8=0`. Gives thinner, more isotropic skeletons.

### K3M

Per iteration phase 0 marks border pixels (`A0`), phases 1-5 delete marked pixels whose weight is in `A1..A5` (sequentially, on the current image). After the iterations a final phase deletes pixels in `A1pix` to get a 1-pixel-wide skeleton. All arrays are taken from Saeed et al., 2010.

### Speed

Algorithms work on flat `byte[]` arrays (no `GetPixel`/`SetPixel`). Every deletion rule is precomputed into a 256-entry lookup table indexed by the neighbourhood mask, so each pixel costs one mask computation and one table read. A test thins a random 1500×1500 image and asserts it finishes in under one second.

## Core API

```csharp
// image: flat 0/1 array with a 1-pixel border, so W = width + 2, H = height + 2
byte[] skeleton = Thinning.ZhangSuen(image, W, H);
byte[] skeleton2 = Thinning.Kmm(image, W, H);
byte[] skeleton3 = Thinning.GuoHall(image, W, H);
byte[] skeleton4 = Thinning.K3M(image, W, H);
byte[] any = Thinning.Run(ThinningAlgorithm.K3M, image, W, H);
```

Both methods return a new array and do not modify the input.

## Tests

xUnit tests run for both algorithms: empty image, single pixel, 1-pixel line unchanged, thick line becomes thin, connectivity of a thick plus sign, two objects stay two, ring keeps its hole, idempotence, input not modified, and a 1500×1500 speed check.

## References

- Zhang, Suen, *A fast parallel algorithm for thinning digital patterns*, Communications of the ACM, 1984.
- Saeed, Tabędzki, Rybnik, Adamski, *K3M: A universal algorithm for image skeletonization and a review of thinning techniques*, 2010 (also describes KMM).
