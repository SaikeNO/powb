namespace Minucje.Core;

public enum MinutiaType
{
    Ending,       // zakończenie,   CN = 1
    Bifurcation,  // rozwidlenie,   CN = 3
    Crossing,     // skrzyżowanie,  CN = 4
    Isolated,     // punkt izolowany, CN = 0
}

public enum RejectionReason
{
    ScannerBorder,  // za blisko brzegu odcisku
    BrokenRidge,    // dwa zakończenia blisko siebie
    Spur,           // zakończenie blisko rozwidlenia
    Bridge,         // dwa rozwidlenia blisko siebie
}

// minucja: położenie w obrazie (bez ramki), typ i wartość CN
public sealed record Minutia(int X, int Y, MinutiaType Type, int CrossingNumber);

public sealed record RejectedMinutia(Minutia Minutia, RejectionReason Reason);
