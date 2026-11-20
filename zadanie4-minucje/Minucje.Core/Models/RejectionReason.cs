namespace Minucje.Core.Models;

// dlaczego minucja została uznana za fałszywą
public enum RejectionReason
{
    ScannerBorder,  // za blisko brzegu odcisku
    BrokenRidge,    // dwa zakończenia blisko siebie
    Spur,           // zakończenie blisko rozwidlenia
    Bridge,         // dwa rozwidlenia blisko siebie
}
