using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;

namespace UI_Unilineal.Playground.Prototype;

/// <summary>
/// SPIKE V0.3.
/// Representación gráfica experimental del detalle de un tablero.
/// No constituye todavía una implementación normativa completa de RIC 18.
/// </summary>
public sealed class RicBoardDetailPrototypeControl : UserControl
{
    private const double HeaderHeight = 92;
    private const double TopologyTop = 108;
    private const double MainX = 82;
    private const double ProtectionWidth = 112;
    private const double ProtectionHeight = 46;
    private const double BusY = 270;
    private const double BranchSpacing = 142;
    private const double BranchStartX = 68;
    private const double CircuitBoxWidth = 122;
    private const double CircuitBoxHeight = 96;

    private static readonly IBrush DiagramStroke =
        new SolidColorBrush(Color.Parse("#263238"));

    private static readonly IBrush SecondaryStroke =
        new SolidColorBrush(Color.Parse("#607D8B"));

    private static readonly IBrush AccentStroke =
        new SolidColorBrush(Color.Parse("#00838F"));

    private static readonly IBrush PendingStroke =
        new SolidColorBrush(Color.Parse("#D18B00"));

    private static readonly IBrush CanvasBackground =
        new SolidColorBrush(Color.Parse("#FAFBFC"));

    private readonly PrototypeBlock _board;
    private readonly IReadOnlyList<PrototypeCircuitBranch> _circuits;
    private readonly string _parentLabel;

    public RicBoardDetailPrototypeControl(
        PrototypeBlock board,
        IReadOnlyList<PrototypeCircuitBranch> circuits,
        string parentLabel)
    {
        _board = board;
        _circuits = circuits;
        _parentLabel = parentLabel;

        var minimumWidth = 520d;
        var circuitsWidth =
            BranchStartX +
            Math.Max(1, circuits.Count) * BranchSpacing +
            60;

        Width = Math.Max(minimumWidth, circuitsWidth);
        Height = 670;

        Content = Build();
    }

    private Control Build()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        root.Children.Add(BuildHeader());

        var canvas = new Canvas
        {
            Background = CanvasBackground,
            MinHeight = 560
        };

        Grid.SetRow(canvas, 1);

        root.Children.Add(canvas);

        DrawBoardDiagram(canvas);

        return root;
    }

    private Control BuildHeader()
    {
        var outer = new Border
        {
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = SecondaryStroke,
            CornerRadius = new CornerRadius(5),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var left = new StackPanel
        {
            Spacing = 3
        };

        left.Children.Add(new TextBlock
        {
            Text = _board.Code,
            FontSize = 20,
            FontWeight = FontWeight.Bold
        });

        left.Children.Add(new TextBlock
        {
            Text = _board.Title,
            FontSize = 13,
            Opacity = 0.75
        });

        left.Children.Add(new TextBlock
        {
            Text = $"Alimentado desde: {_parentLabel}",
            FontSize = 12
        });

        grid.Children.Add(left);

        var badge = new Border
        {
            Padding = new Thickness(9, 5),
            BorderThickness = new Thickness(1),
            BorderBrush = PendingStroke,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Child = new TextBlock
            {
                Text = "RIC18 · REFERENCIA",
                FontSize = 10,
                FontWeight = FontWeight.SemiBold
            }
        };

        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        outer.Child = grid;

        return outer;
    }

    private void DrawBoardDiagram(Canvas canvas)
    {
        if (_board.Kind == PrototypeBlockKind.ServiceEntrance)
        {
            DrawServiceEntrance(canvas);
            return;
        }

        DrawIncomingFeeder(canvas);
        DrawMainProtection(canvas);
        DrawBus(canvas);
        DrawCircuitBranches(canvas);
        DrawGroundingReference(canvas);
        DrawLegend(canvas);
    }

    private void DrawIncomingFeeder(Canvas canvas)
    {
        AddText(
            canvas,
            $"ALIMENTACIÓN\n{_parentLabel}",
            14,
            12,
            fontSize: 11,
            bold: true);

        AddLine(
            canvas,
            MainX + (ProtectionWidth / 2),
            TopologyTop - 2,
            MainX + (ProtectionWidth / 2),
            158,
            DiagramStroke,
            2);

        AddCircle(
            canvas,
            MainX + (ProtectionWidth / 2),
            158,
            4,
            AccentStroke);
    }

    private void DrawMainProtection(Canvas canvas)
    {
        var x = MainX;
        var y = 162d;

        AddBreakerSymbol(
            canvas,
            x,
            y,
            ProtectionWidth,
            ProtectionHeight);

        AddText(
            canvas,
            "TM GENERAL",
            x + 8,
            y + 5,
            fontSize: 10,
            bold: true);

        AddText(
            canvas,
            _board.Kind == PrototypeBlockKind.MainBoard
                ? "4P · 100 A · C"
                : "4P · 40 A · C",
            x + 8,
            y + 21,
            fontSize: 10);

        AddText(
            canvas,
            "Icu: POR DEFINIR",
            x + 8,
            y + 34,
            fontSize: 9,
            brush: PendingStroke);

        AddLine(
            canvas,
            x + (ProtectionWidth / 2),
            y + ProtectionHeight,
            x + (ProtectionWidth / 2),
            BusY,
            DiagramStroke,
            2);
    }

    private void DrawBus(Canvas canvas)
    {
        var branchCount = Math.Max(1, _circuits.Count);
        var busStart = MainX + (ProtectionWidth / 2);
        var busEnd =
            BranchStartX +
            ((branchCount - 1) * BranchSpacing) +
            (CircuitBoxWidth / 2);

        busEnd = Math.Max(busStart + 150, busEnd);

        AddLine(
            canvas,
            busStart,
            BusY,
            busEnd,
            BusY,
            DiagramStroke,
            6);

        AddText(
            canvas,
            "BARRA PRINCIPAL",
            busStart + 12,
            BusY - 25,
            fontSize: 10,
            bold: true);
    }

    private void DrawCircuitBranches(Canvas canvas)
    {
        if (_circuits.Count == 0)
        {
            AddText(
                canvas,
                "Sin circuitos ficticios definidos.",
                BranchStartX,
                BusY + 40,
                fontSize: 12,
                brush: SecondaryStroke);

            return;
        }

        for (var index = 0; index < _circuits.Count; index++)
        {
            var circuit = _circuits[index];

            var x =
                BranchStartX +
                (index * BranchSpacing);

            DrawCircuitBranch(
                canvas,
                circuit,
                x,
                BusY);
        }
    }

    private void DrawCircuitBranch(
        Canvas canvas,
        PrototypeCircuitBranch circuit,
        double x,
        double busY)
    {
        var centerX = x + (CircuitBoxWidth / 2);
        var breakerY = busY + 44;
        var differentialY = breakerY + 72;
        var loadY = differentialY + 80;

        AddLine(
            canvas,
            centerX,
            busY,
            centerX,
            breakerY,
            DiagramStroke,
            2);

        AddCircle(
            canvas,
            centerX,
            busY,
            3.5,
            DiagramStroke);

        AddBreakerSymbol(
            canvas,
            x + 14,
            breakerY,
            CircuitBoxWidth - 28,
            42);

        AddText(
            canvas,
            $"{circuit.Code} · TM",
            x + 22,
            breakerY + 5,
            fontSize: 10,
            bold: true);

        AddText(
            canvas,
            CompactBreakerText(circuit.Breaker),
            x + 22,
            breakerY + 21,
            fontSize: 9);

        AddLine(
            canvas,
            centerX,
            breakerY + 42,
            centerX,
            differentialY,
            DiagramStroke,
            2);

        DrawDifferentialSymbol(
            canvas,
            x + 14,
            differentialY,
            CircuitBoxWidth - 28,
            42);

        AddText(
            canvas,
            "ID",
            x + 22,
            differentialY + 5,
            fontSize: 10,
            bold: true);

        AddText(
            canvas,
            CompactDifferentialText(circuit.Differential),
            x + 22,
            differentialY + 21,
            fontSize: 9);

        AddLine(
            canvas,
            centerX,
            differentialY + 42,
            centerX,
            loadY,
            DiagramStroke,
            2);

        var isDownstreamBoard =
            circuit.Name.StartsWith(
                "Alimentador ",
                StringComparison.OrdinalIgnoreCase);

        DrawLoadBox(
            canvas,
            circuit,
            x,
            loadY,
            isDownstreamBoard);
    }

    private void DrawLoadBox(
        Canvas canvas,
        PrototypeCircuitBranch circuit,
        double x,
        double y,
        bool downstreamBoard)
    {
        var border = new Border
        {
            Width = CircuitBoxWidth,
            Height = CircuitBoxHeight,
            Padding = new Thickness(7),
            BorderThickness = new Thickness(
                downstreamBoard ? 2 : 1),
            BorderBrush =
                downstreamBoard
                    ? AccentStroke
                    : SecondaryStroke,
            Background =
                downstreamBoard
                    ? new SolidColorBrush(Color.Parse("#E0F7FA"))
                    : Brushes.White,
            CornerRadius = new CornerRadius(3)
        };

        var stack = new StackPanel
        {
            Spacing = 2
        };

        stack.Children.Add(new TextBlock
        {
            Text = circuit.Code,
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = DiagramStroke
        });

        stack.Children.Add(new TextBlock
        {
            Text =
                downstreamBoard
                    ? "TABLERO DERIVADO"
                    : circuit.Name,
            FontSize = 9,
            FontWeight =
                downstreamBoard
                    ? FontWeight.SemiBold
                    : FontWeight.Normal,
            Foreground = DiagramStroke,
            TextWrapping = TextWrapping.Wrap
        });

        if (downstreamBoard)
        {
            stack.Children.Add(new TextBlock
            {
                Text = circuit.Name.Replace(
                    "Alimentador ",
                    string.Empty,
                    StringComparison.OrdinalIgnoreCase),
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = AccentStroke
            });
        }

        stack.Children.Add(new TextBlock
        {
            Text = circuit.Conductor,
            FontSize = 9,
            Foreground = DiagramStroke,
            TextWrapping = TextWrapping.Wrap
        });

        border.Child = stack;

        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);

        canvas.Children.Add(border);
    }

    private void DrawGroundingReference(Canvas canvas)
    {
        var x = 18d;
        var y = 430d;

        AddText(
            canvas,
            "PUESTA A TIERRA",
            x,
            y,
            fontSize: 10,
            bold: true);

        AddLine(
            canvas,
            x + 48,
            y + 22,
            x + 48,
            y + 55,
            DiagramStroke,
            2);

        AddLine(
            canvas,
            x + 28,
            y + 55,
            x + 68,
            y + 55,
            DiagramStroke,
            2);

        AddLine(
            canvas,
            x + 34,
            y + 62,
            x + 62,
            y + 62,
            DiagramStroke,
            2);

        AddLine(
            canvas,
            x + 40,
            y + 69,
            x + 56,
            y + 69,
            DiagramStroke,
            2);

        AddText(
            canvas,
            "Datos: POR DEFINIR",
            x,
            y + 79,
            fontSize: 9,
            brush: PendingStroke);
    }

    private void DrawLegend(Canvas canvas)
    {
        var y = 528d;

        AddText(
            canvas,
            "SPIKE V0.3 · composición gráfica experimental",
            18,
            y,
            fontSize: 10,
            bold: true,
            brush: SecondaryStroke);

        AddText(
            canvas,
            "Las geometrías y distancias son APP_CONVENTION. " +
            "No se afirma cumplimiento gráfico completo RIC 18.",
            18,
            y + 18,
            fontSize: 9,
            brush: SecondaryStroke);
    }

    private void DrawServiceEntrance(Canvas canvas)
    {
        AddText(
            canvas,
            "EMPALME / ORIGEN",
            24,
            30,
            fontSize: 14,
            bold: true);

        AddText(
            canvas,
            "Vista experimental. El detalle de empalme se modelará " +
            "como bloque especializado en etapas posteriores.",
            24,
            58,
            fontSize: 11,
            brush: SecondaryStroke);

        AddLine(
            canvas,
            130,
            125,
            130,
            250,
            DiagramStroke,
            3);

        AddCircle(
            canvas,
            130,
            125,
            7,
            AccentStroke);

        AddText(
            canvas,
            "RED / ACOMETIDA",
            156,
            116,
            fontSize: 11,
            bold: true);

        AddBreakerSymbol(
            canvas,
            74,
            255,
            112,
            50);

        AddText(
            canvas,
            "PROTECCIÓN",
            90,
            266,
            fontSize: 10,
            bold: true);

        AddText(
            canvas,
            "Datos por definir",
            90,
            282,
            fontSize: 9,
            brush: PendingStroke);

        AddLine(
            canvas,
            130,
            305,
            130,
            390,
            DiagramStroke,
            3);

        AddText(
            canvas,
            "SALIDA A TABLERO GENERAL",
            156,
            372,
            fontSize: 11,
            bold: true);

        DrawLegend(canvas);
    }

    private static void AddBreakerSymbol(
        Canvas canvas,
        double x,
        double y,
        double width,
        double height)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            BorderThickness = new Thickness(1.5),
            BorderBrush = DiagramStroke,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(2)
        };

        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);

        canvas.Children.Add(border);

        AddLine(
            canvas,
            x + width - 26,
            y + 9,
            x + width - 11,
            y + height - 9,
            DiagramStroke,
            2);
    }

    private static void DrawDifferentialSymbol(
        Canvas canvas,
        double x,
        double y,
        double width,
        double height)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            BorderThickness = new Thickness(1.5),
            BorderBrush = DiagramStroke,
            Background =
                new SolidColorBrush(Color.Parse("#F7F7F7")),
            CornerRadius = new CornerRadius(2)
        };

        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);

        canvas.Children.Add(border);

        var circle = new Ellipse
        {
            Width = 17,
            Height = 17,
            Stroke = AccentStroke,
            StrokeThickness = 1.5
        };

        Canvas.SetLeft(circle, x + width - 28);
        Canvas.SetTop(circle, y + 12);

        canvas.Children.Add(circle);
    }

    private static void AddText(
        Canvas canvas,
        string text,
        double x,
        double y,
        double fontSize,
        bool bold = false,
        IBrush? brush = null)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight =
                bold
                    ? FontWeight.SemiBold
                    : FontWeight.Normal,
            Foreground = brush ?? DiagramStroke,
            TextWrapping = TextWrapping.Wrap
        };

        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);

        canvas.Children.Add(block);
    }

    private static void AddLine(
        Canvas canvas,
        double x1,
        double y1,
        double x2,
        double y2,
        IBrush brush,
        double thickness)
    {
        canvas.Children.Add(new Line
        {
            StartPoint = new Point(x1, y1),
            EndPoint = new Point(x2, y2),
            Stroke = brush,
            StrokeThickness = thickness
        });
    }

    private static void AddCircle(
        Canvas canvas,
        double centerX,
        double centerY,
        double radius,
        IBrush brush)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = Brushes.White,
            Stroke = brush,
            StrokeThickness = 2
        };

        Canvas.SetLeft(ellipse, centerX - radius);
        Canvas.SetTop(ellipse, centerY - radius);

        canvas.Children.Add(ellipse);
    }

    private static string CompactBreakerText(string source)
    {
        return source
            .Replace("TM ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" · ", " | ");
    }

    private static string CompactDifferentialText(string source)
    {
        return source
            .Replace("ID ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" · ", " | ");
    }
}