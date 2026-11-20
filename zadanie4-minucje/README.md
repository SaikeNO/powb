# Minutiae extraction (Crossing Number): Image Processing in Biometrics

C# / .NET 10 Windows desktop assignment. Fingerprint minutiae are extracted with the **Crossing Number** method and highlighted on the image. Two fingerprints are shown side by side with the number of minutiae of each type, and false minutiae (e.g. on the scanner border) can be removed. Everything is implemented from scratch; WPF is used only for the UI and image decoding.

A Polish walkthrough of the code, for presenting it, is in [MINUCJE.md](MINUCJE.md).

## Build and run

Requirements: Windows and [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build Minucje.slnx
dotnet run --project Minucje.App
dotnet test Minucje.slnx
```

Projects: `Minucje.Core` (`net10.0`) contains the algorithms, `Minucje.App` (`net10.0-windows`) contains the WPF UI and drawing, `Minucje.Tests` contains xUnit tests.

## Using the app

- Fingerprints **A** (`1_1`) and **B** (`1_2`, the same finger) load at start. Pick another sample from the list, use **Open…**, or drag an image onto a panel.
- Minutiae are marked as follows: ending = red circle, bifurcation = blue square, crossing = purple diamond, isolated point = orange triangle.
- The right panel shows the number of each minutia type for A and B. Hover a row to see what that type means.
- **False minutiae**: turn the filter on or off, set the border margin and the minimum distance, and show or hide the rejected minutiae (grey ×). The area outside the fingerprint is tinted yellow.
- **View**: original, skeleton or binary image. **Save…** writes the marked image as PNG.

## Pipeline (`FingerprintAnalyzer.Analyze`)

1. **Mask** (`Preprocessing.ComputeMask`): grey-level standard deviation in 8×8 blocks above 30% of the maximum. Holes are filled by row and by column.
2. **Binarization** (`Preprocessing.Binarize`): 5×5 blur, then a pixel is a ridge if it is darker than the mean of its 15×15 window.
3. **Thinning** (`K3M.Thin`): K3M from task 3.
4. **Crossing Number** (`CrossingNumber`): `CN = ½ · Σ |P(i) − P(i+1)|` over the 8 neighbours in clockwise order. CN 0 = isolated point, 1 = ending, 2 = ridge pixel, 3 = bifurcation, 4 = crossing. Same-type minutiae within 2 px are merged.
5. **False minutiae** (`MinutiaeFilter`): a minutia is rejected if the scanner background or the image edge is within *border margin* (16 px). Pairs closer than *min. distance* (8 px) are also rejected: ending + ending (broken ridge), ending + bifurcation (spur), bifurcation + bifurcation (bridge).

## Tests

CN for chosen neighbourhoods. A line gives 2 endings, a Y gives 1 bifurcation, an X gives 1 crossing, a single pixel is an isolated point and a ring has no minutiae. The border and close-pair filters are tested separately. The whole pipeline runs on a synthetic ridge image: without the filter every ridge cut at the image edge gives an ending, and with the filter only the 2 real endings and 2 bifurcations remain.

## References

- Rutovitz, *Pattern recognition*, Journal of the Royal Statistical Society, 1966 (Crossing Number).
- Maltoni, Maio, Jain, Prabhakar, *Handbook of Fingerprint Recognition*, Springer, 2009.
- Saeed, Tabędzki, Rybnik, Adamski, *K3M: A universal algorithm for image skeletonization and a review of thinning techniques*, 2010.
