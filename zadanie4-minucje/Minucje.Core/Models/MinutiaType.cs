namespace Minucje.Core.Models;

public enum MinutiaType
{
    Ending,       // zakończenie - linia papilarna się kończy, CN = 1
    Bifurcation,  // rozwidlenie - linia dzieli się na dwie, CN = 3
    Crossing,     // skrzyżowanie - dwie linie się przecinają, CN = 4
    Isolated,     // punkt izolowany - pojedyncza kropka bez sąsiadów, CN = 0
}
