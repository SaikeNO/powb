namespace Minucje.Core.Models;

// minucja: położenie w obrazie (bez ramki), typ i wartość CN
public sealed record Minutia(int X, int Y, MinutiaType Type, int CrossingNumber);
