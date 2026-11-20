using System.Windows;
using System.Windows.Media;
using Minucje.Core;

namespace Minucje;

// Wygląd i nazwy minucji: kolor znacznika, polska nazwa i opis do podpowiedzi.
// Te same znaczniki rysuje MinutiaeRenderer na obrazie i tabela po prawej jako ikony.
public static class Legend
{
    public static readonly Color RejectedColor = Color.FromRgb(120, 113, 108); // ciepły szary

    public static Color ColorOf(MinutiaType type)
    {
        switch (type)
        {
            case MinutiaType.Ending: return Color.FromRgb(220, 38, 38);       // czerwony
            case MinutiaType.Bifurcation: return Color.FromRgb(37, 99, 235);  // niebieski
            case MinutiaType.Crossing: return Color.FromRgb(192, 38, 211);    // fioletowy
            default: return Color.FromRgb(22, 163, 74);                       // zielony
        }
    }

    public static string NameOf(MinutiaType type)
    {
        switch (type)
        {
            case MinutiaType.Ending: return "Zakończenie";
            case MinutiaType.Bifurcation: return "Rozwidlenie";
            case MinutiaType.Crossing: return "Skrzyżowanie";
            default: return "Punkt izolowany";
        }
    }

    public static string CrossingNumberOf(MinutiaType type)
    {
        switch (type)
        {
            case MinutiaType.Ending: return "1";
            case MinutiaType.Bifurcation: return "3";
            case MinutiaType.Crossing: return "4";
            default: return "0";
        }
    }

    public static string DescriptionOf(MinutiaType type)
    {
        switch (type)
        {
            case MinutiaType.Ending: return "Linia papilarna się kończy (jedna linia wychodzi z punktu).";
            case MinutiaType.Bifurcation: return "Linia dzieli się na dwie (trzy linie wychodzą z punktu).";
            case MinutiaType.Crossing: return "Dwie linie się przecinają (cztery linie wychodzą z punktu).";
            default: return "Pojedynczy punkt bez sąsiadów (kropka).";
        }
    }

    public static string NameOf(RejectionReason reason)
    {
        switch (reason)
        {
            case RejectionReason.ScannerBorder: return "Brzeg skanera";
            case RejectionReason.BrokenRidge: return "Przerwana linia";
            case RejectionReason.Spur: return "Ostroga";
            default: return "Mostek / dziura";
        }
    }

    public static string DescriptionOf(RejectionReason reason)
    {
        switch (reason)
        {
            case RejectionReason.ScannerBorder: return "Linia ucięta na granicy odcisku daje fałszywe zakończenie.";
            case RejectionReason.BrokenRidge: return "Dwa zakończenia blisko siebie: linia przerwana przez szum.";
            case RejectionReason.Spur: return "Zakończenie przy rozwidleniu: krótkie fałszywe odgałęzienie.";
            default: return "Dwa rozwidlenia blisko siebie: mostek między liniami albo dziura w linii.";
        }
    }

    // znacznik: kolorowy okrąg, kolor zależy od typu minucji
    public static void DrawMarker(DrawingContext context, MinutiaType type, Point center, double size)
    {
        var pen = new Pen(new SolidColorBrush(ColorOf(type)), size / 4);
        context.DrawEllipse(null, pen, center, size / 2, size / 2);
    }

    // odrzucona minucja: szary × z białą obwódką - widoczny na liniach, ale nie odciąga uwagi od prawdziwych
    public static void DrawRejectedMarker(DrawingContext context, Point center, double size)
    {
        double thickness = size / 5;
        DrawCross(context, new Pen(Brushes.White, thickness + 1.5), center, size / 2);
        DrawCross(context, new Pen(new SolidColorBrush(RejectedColor), thickness), center, size / 2);
    }

    private static void DrawCross(DrawingContext context, Pen pen, Point center, double r)
    {
        context.DrawLine(pen, new Point(center.X - r, center.Y - r), new Point(center.X + r, center.Y + r));
        context.DrawLine(pen, new Point(center.X - r, center.Y + r), new Point(center.X + r, center.Y - r));
    }

    public static ImageSource MarkerIcon(MinutiaType type)
    {
        return Icon(context => DrawMarker(context, type, new Point(8, 8), 10));
    }

    public static ImageSource RejectedIcon()
    {
        return Icon(context => DrawRejectedMarker(context, new Point(8, 8), 10));
    }

    // ikona 16×16 do tabeli
    private static ImageSource Icon(Action<DrawingContext> draw)
    {
        var drawing = new DrawingGroup();
        using (DrawingContext context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 16, 16));
            draw(context);
        }
        return new DrawingImage(drawing);
    }
}
