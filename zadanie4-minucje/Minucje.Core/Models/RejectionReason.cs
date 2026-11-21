namespace Minucje.Core.Models;

public enum RejectionReason
{
    ScannerBorder,  // linia ucięta na krawędzi odcisku wygląda jak zakończenie
    BrokenRidge,    // dwa zakończenia blisko siebie
    Spur,           // oszakończenie blisko rozwidlenia
    Bridge,         // dwa rozwidlenia blisko siebie
}
