namespace Minucje.Core.Models;

// typ minucji wynikający z wartości Crossing Number
public enum MinutiaType
{
    Ending,       // zakończenie,     CN = 1
    Bifurcation,  // rozwidlenie,     CN = 3
    Crossing,     // skrzyżowanie,    CN = 4
    Isolated,     // punkt izolowany, CN = 0
}
