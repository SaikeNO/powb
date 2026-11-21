namespace Minucje.Core.Models;

public enum RejectionReason
{
    ScannerBorder,  // brzeg skanera - linia ucięta na krawędzi odcisku wygląda jak zakończenie
    BrokenRidge,    // przerwana linia - dwa zakończenia blisko siebie (przerwa w linii przez szum)
    Spur,           // ostroga - zakończenie blisko rozwidlenia (krótkie fałszywe odgałęzienie)
    Bridge,         // mostek - dwa rozwidlenia blisko siebie (linie zlane w jednym miejscu albo dziura w linii)
}
