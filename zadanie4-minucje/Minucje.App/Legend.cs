using System.Windows;
using System.Windows.Media;
using Minucje.Core;

namespace Minucje;

// Wygląd i nazwy minucji: kolor, kształt znacznika, polska nazwa i opis do podpowiedzi.
// Te same znaczniki rysuje MinutiaeRenderer na obrazie i tabela po prawej jako ikony.
public static class Legend
{
    public static readonly Color RejectedColor = Color.FromRgb(107, 114, 128);

    public static Color ColorOf(MinutiaType type)
    {
        switch (type)
        {
            case MinutiaType.Ending: return Color.FromRgb(220, 38, 38);       // czerwony
            case MinutiaType.Bifurcation: return Color.FromRgb(37, 99, 235);  // niebieski
            case MinutiaType.Crossing: return Color.FromRgb(192, 38, 211);    // fioletowy
            default: return Color.FromRgb(234, 88, 12);                       // pomarańczowy
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

    // znacznik: kółko = zakończenie, kwadrat = rozwidlenie, romb = skrzyżowanie, trójkąt = punkt izolowany
    public static void DrawMarker(DrawingContext context, MinutiaType type, Point center, double size)
    {
        var pen = new Pen(new SolidColorBrush(ColorOf(type)), size / 4);
        double r = size / 2;
        switch (type)
        {
            case MinutiaType.Ending:
                context.DrawEllipse(null, pen, center, r, r);
                break;
            case MinutiaType.Bifurcation:
                context.DrawRectangle(null, pen, new Rect(center.X - r, center.Y - r, size, size));
                break;
            case MinutiaType.Crossing:
                DrawPolygon(context, pen,
                    new Point(center.X, center.Y - r * 1.3), new Point(center.X + r * 1.3, center.Y),
                    new Point(center.X, center.Y + r * 1.3), new Point(center.X - r * 1.3, center.Y));
                break;
            default:
                DrawPolygon(context, pen,
                    new Point(center.X, center.Y - r * 1.2), new Point(center.X + r * 1.1, center.Y + r * 0.7),
                    new Point(center.X - r * 1.1, center.Y + r * 0.7));
                break;
        }
    }

    public static void DrawRejectedMarker(DrawingContext context, Point center, double size)
    {
        var pen = new Pen(new SolidColorBrush(RejectedColor), size / 5);
        double r = size / 2;
        context.DrawLine(pen, new Point(center.X - r, center.Y - r), new Point(center.X + r, center.Y + r));
        context.DrawLine(pen, new Point(center.X - r, center.Y + r), new Point(center.X + r, center.Y - r));
    }

    public static ImageSource MarkerIcon(MinutiaType type)
    {
        return Icon(context => DrawMarker(context, type, new Point(8, 8), 10));
    }

    public static ImageSource RejectedIcon()
    {
        return Icon(context => DrawRejectedMarker(context, new Point(8, 8), 8));
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

    private static void DrawPolygon(DrawingContext context, Pen pen, params Point[] corners)
    {
        var figure = new PathFigure { StartPoint = corners[0], IsClosed = true };
        for (int i = 1; i < corners.Length; i++)
        {
            figure.Segments.Add(new LineSegment(corners[i], isStroked: true));
        }
        context.DrawGeometry(null, pen, new PathGeometry(new[] { figure }));
    }
}
