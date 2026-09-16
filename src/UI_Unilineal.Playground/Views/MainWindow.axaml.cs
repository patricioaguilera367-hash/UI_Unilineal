using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using UI_Unilineal.Playground.Prototype;

namespace UI_Unilineal.Playground.Views;

public sealed partial class MainWindow : Window
{
    private const double BlockWidth = 184;
    private const double BlockHeight = 104;
    private const double TerminalRadius = 5;

    private readonly PrototypeDiagramState _state =
        PrototypeDiagramState.CreateDemo();

    private readonly Dictionary<string, Border> _blockControls = new();

    private readonly ObservableCollection<string> _events = new();

    private PrototypeInteractionMode _mode =
        PrototypeInteractionMode.Layout;

    private string? _selectedUid;
    private string? _draggedUid;

    private Point _dragPointerStart;
    private double _dragBlockStartX;
    private double _dragBlockStartY;
    private bool _dragMoved;

    public MainWindow()
    {
        InitializeComponent();

        EventLogList.ItemsSource = _events;

        LayoutModeButton.Click += (_, _) =>
            SetMode(PrototypeInteractionMode.Layout);

        ElectricalModeButton.Click += (_, _) =>
            SetMode(PrototypeInteractionMode.Electrical);

        BuildSummaryBlocks();
        DrawConnections();

        SelectBlock("BOARD:TDA-01");
        SetMode(PrototypeInteractionMode.Layout);

        AddLog(
            "Spike cargado · ningún cambio se persiste en ProyectoElectrico.");
    }

    private void SetMode(PrototypeInteractionMode mode)
    {
        _mode = mode;

        ModeText.Text = mode == PrototypeInteractionMode.Layout
            ? "Presentación"
            : "Eléctrico";

        AddLog(
            mode == PrototypeInteractionMode.Layout
                ? "Modo presentación · mover bloques no cambia relaciones eléctricas."
                : "Modo eléctrico · arrastra un tablero sobre otro para cambiar su alimentación ficticia.");
    }

    private void BuildSummaryBlocks()
    {
        DiagramCanvas.Children.Clear();
        _blockControls.Clear();

        foreach (var block in _state.Blocks)
        {
            var control = CreateBlockControl(block);

            _blockControls.Add(block.Uid, control);

            Canvas.SetLeft(control, block.X);
            Canvas.SetTop(control, block.Y);

            DiagramCanvas.Children.Add(control);
        }
    }

    private Border CreateBlockControl(PrototypeBlock block)
    {
        var title = new TextBlock
        {
            Text = block.Code,
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White
        };

        var subtitle = new TextBlock
        {
            Text = block.Title,
            FontSize = 12,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap
        };

        var role = new TextBlock
        {
            Text = GetRoleText(block.Kind),
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };

        var feeder = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(block.FeederCode)
                ? "Alimentador: ORIGEN"
                : $"Alimentador: {block.FeederCode}",
            FontSize = 11,
            Foreground = Brushes.White
        };

        var content = new StackPanel
        {
            Spacing = 3
        };

        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(role);
        content.Children.Add(feeder);

        var border = new Border
        {
            Width = BlockWidth,
            Height = BlockHeight,
            Padding = new Thickness(10),
            BorderThickness = new Thickness(2),
            BorderBrush = GetBlockAccent(block.Kind),
            Background = GetBlockBrush(block.Kind),
            CornerRadius = new CornerRadius(6),
            Child = content
        };

        border.PointerPressed += (_, e) =>
            OnBlockPointerPressed(block.Uid, border, e);

        border.PointerMoved += (_, e) =>
            OnBlockPointerMoved(block.Uid, border, e);

        border.PointerReleased += (_, e) =>
            OnBlockPointerReleased(block.Uid, border, e);

        return border;
    }

    private void OnBlockPointerPressed(
        string uid,
        Border control,
        PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(DiagramCanvas);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _draggedUid = uid;
        _dragPointerStart = point.Position;

        var block = _state.GetBlock(uid);

        _dragBlockStartX = block.X;
        _dragBlockStartY = block.Y;
        _dragMoved = false;

        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void OnBlockPointerMoved(
        string uid,
        Border control,
        PointerEventArgs e)
    {
        if (_draggedUid != uid)
        {
            return;
        }

        var point = e.GetCurrentPoint(DiagramCanvas);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var delta = point.Position - _dragPointerStart;

        if (Math.Abs(delta.X) + Math.Abs(delta.Y) > 3)
        {
            _dragMoved = true;
        }

        var x = Math.Max(0, _dragBlockStartX + delta.X);
        var y = Math.Max(0, _dragBlockStartY + delta.Y);

        Canvas.SetLeft(control, x);
        Canvas.SetTop(control, y);

        if (_mode == PrototypeInteractionMode.Layout)
        {
            var block = _state.GetBlock(uid);
            block.X = x;
            block.Y = y;

            DrawConnections();
        }

        e.Handled = true;
    }

    private void OnBlockPointerReleased(
        string uid,
        Border control,
        PointerReleasedEventArgs e)
    {
        if (_draggedUid != uid)
        {
            return;
        }

        var releasePosition = e.GetPosition(DiagramCanvas);

        e.Pointer.Capture(null);

        if (!_dragMoved)
        {
            SelectBlock(uid);
            ClearDrag();
            return;
        }

        if (_mode == PrototypeInteractionMode.Layout)
        {
            var block = _state.GetBlock(uid);

            var command = _state.MoveBlock(
                uid,
                block.X,
                block.Y);

            AddLog(command.Description);
            SelectBlock(uid);
        }
        else
        {
            HandleElectricalDrop(uid, control, releasePosition);
        }

        ClearDrag();
    }

    private void HandleElectricalDrop(
        string draggedUid,
        Border draggedControl,
        Point releasePosition)
    {
        var draggedBlock = _state.GetBlock(draggedUid);

        Canvas.SetLeft(draggedControl, _dragBlockStartX);
        Canvas.SetTop(draggedControl, _dragBlockStartY);

        draggedBlock.X = _dragBlockStartX;
        draggedBlock.Y = _dragBlockStartY;

        if (draggedBlock.Kind == PrototypeBlockKind.ServiceEntrance)
        {
            AddLog("RECHAZADO · el empalme no puede cambiar de alimentación.");
            DrawConnections();
            return;
        }

        var target = FindDropTarget(
            releasePosition,
            draggedUid);

        if (target is null)
        {
            AddLog(
                $"Sin cambio · {draggedBlock.Code} no fue soltado sobre otro bloque.");

            DrawConnections();
            return;
        }

        try
        {
            var command = _state.ChangeSupply(
                draggedUid,
                target.Uid);

            AddLog(command.Description);
            SelectBlock(draggedUid);
        }
        catch (InvalidOperationException exception)
        {
            AddLog($"RECHAZADO · {exception.Message}");
        }

        DrawConnections();
    }

    private PrototypeBlock? FindDropTarget(
        Point position,
        string draggedUid)
    {
        foreach (var block in _state.Blocks)
        {
            if (block.Uid == draggedUid)
            {
                continue;
            }

            var bounds = new Rect(
                block.X,
                block.Y,
                BlockWidth,
                BlockHeight);

            if (bounds.Contains(position))
            {
                return block;
            }
        }

        return null;
    }

    private void DrawConnections()
    {
        ConnectionCanvas.Children.Clear();

        foreach (var block in _state.Blocks)
        {
            AddTerminal(block, isInput: true);
            AddTerminal(block, isInput: false);
        }

        foreach (var child in _state.Blocks)
        {
            if (child.ParentUid is null)
            {
                continue;
            }

            var parent = _state.GetBlock(child.ParentUid);

            var start = new Point(
                parent.X + BlockWidth,
                parent.Y + (BlockHeight / 2));

            var end = new Point(
                child.X,
                child.Y + (BlockHeight / 2));

            var midX = start.X + ((end.X - start.X) / 2);

            AddConnectionSegment(
                start,
                new Point(midX, start.Y));

            AddConnectionSegment(
                new Point(midX, start.Y),
                new Point(midX, end.Y));

            AddConnectionSegment(
                new Point(midX, end.Y),
                end);

            AddArrowhead(end, GetBlockAccent(child.Kind));

            if (!string.IsNullOrWhiteSpace(child.FeederCode))
            {
                var label = new TextBlock
                {
                    Text = $"{child.FeederCode} · entrada",
                    FontSize = 11,
                    Foreground = Brushes.DarkSlateGray,
                    Background = Brushes.White,
                    Padding = new Thickness(4, 1)
                };

                Canvas.SetLeft(
                    label,
                    midX + 5);

                Canvas.SetTop(
                    label,
                    ((start.Y + end.Y) / 2) - 10);

                ConnectionCanvas.Children.Add(label);
            }
        }
    }

    private void AddConnectionSegment(
        Point start,
        Point end)
    {
        var line = new Line
        {
            StartPoint = start,
            EndPoint = end,
            Stroke = Brushes.DarkSlateGray,
            StrokeThickness = 2
        };

        ConnectionCanvas.Children.Add(line);
    }

    private void AddTerminal(PrototypeBlock block, bool isInput)
    {
        var terminal = new Ellipse
        {
            Width = TerminalRadius * 2,
            Height = TerminalRadius * 2,
            Fill = Brushes.White,
            Stroke = GetBlockAccent(block.Kind),
            StrokeThickness = 2
        };

        var x = isInput
            ? block.X - TerminalRadius
            : block.X + BlockWidth - TerminalRadius;
        var y = block.Y + (BlockHeight / 2) - TerminalRadius;

        Canvas.SetLeft(terminal, x);
        Canvas.SetTop(terminal, y);
        ConnectionCanvas.Children.Add(terminal);
    }

    private void AddArrowhead(Point end, IBrush brush)
    {
        var arrowhead = new Polygon
        {
            Points = new Points
            {
                end,
                new Point(end.X - 10, end.Y - 6),
                new Point(end.X - 10, end.Y + 6)
            },
            Fill = brush
        };

        ConnectionCanvas.Children.Add(arrowhead);
    }

    private static IBrush GetBlockBrush(PrototypeBlockKind kind)
    {
        return kind switch
        {
            PrototypeBlockKind.ServiceEntrance => Brushes.DarkGoldenrod,
            PrototypeBlockKind.MainBoard => Brushes.DarkSlateBlue,
            _ => Brushes.Teal
        };
    }

    private static IBrush GetBlockAccent(PrototypeBlockKind kind)
    {
        return kind switch
        {
            PrototypeBlockKind.ServiceEntrance => Brushes.Goldenrod,
            PrototypeBlockKind.MainBoard => Brushes.MediumSlateBlue,
            _ => Brushes.DarkCyan
        };
    }

    private void SelectBlock(string uid)
    {
        _selectedUid = uid;

        foreach (var pair in _blockControls)
        {
            pair.Value.BorderBrush =
                pair.Key == uid
                    ? Brushes.DodgerBlue
                    : Brushes.DimGray;

            pair.Value.BorderThickness =
                pair.Key == uid
                    ? new Thickness(3)
                    : new Thickness(2);
        }

        ShowDetail(_state.GetBlock(uid));
    }

    private void ShowDetail(PrototypeBlock block)
    {
        DetailPanel.Children.Clear();

        var header = new Border
        {
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.DimGray,
            CornerRadius = new CornerRadius(5)
        };

        var headerStack = new StackPanel
        {
            Spacing = 4
        };

        headerStack.Children.Add(
            new TextBlock
            {
                Text = block.Code,
                FontSize = 20,
                FontWeight = FontWeight.SemiBold
            });

        headerStack.Children.Add(
            new TextBlock
            {
                Text = block.Title,
                FontSize = 13,
                Opacity = 0.72
            });

        headerStack.Children.Add(
            new TextBlock
            {
                Text = $"UID: {block.Uid}",
                FontSize = 11,
                Opacity = 0.58
            });

        headerStack.Children.Add(
            new TextBlock
            {
                Text = $"Alimentado desde: {GetParentLabel(block)}",
                FontSize = 12
            });

        header.Child = headerStack;

        DetailPanel.Children.Add(header);

        if (block.Kind == PrototypeBlockKind.ServiceEntrance)
        {
            DetailPanel.Children.Add(
                CreateInfoBlock(
                    "EMPALME / FUENTE",
                    "Representación preliminar del origen del proyecto."));

            DetailPanel.Children.Add(
                CreateBusBlock("SALIDA A TABLERO GENERAL"));

            return;
        }

        DetailPanel.Children.Add(
            CreateInfoBlock(
                "PROTECCIÓN GENERAL",
                block.Kind == PrototypeBlockKind.MainBoard
                    ? "TM 4P 100 A · C · Icu POR DEFINIR"
                    : "TM 4P 40 A · C · Icu POR DEFINIR"));

        DetailPanel.Children.Add(
            CreateBusBlock("BARRA PRINCIPAL"));

        var circuits = _state.GetCircuits(block.Uid);

        if (circuits.Count == 0)
        {
            DetailPanel.Children.Add(
                new TextBlock
                {
                    Text = "Sin circuitos ficticios definidos para este tablero.",
                    Opacity = 0.65
                });

            return;
        }

        foreach (var circuit in circuits)
        {
            DetailPanel.Children.Add(
                CreateCircuitBlock(circuit));
        }
    }

    private static Border CreateInfoBlock(
        string title,
        string description)
    {
        var stack = new StackPanel
        {
            Spacing = 3
        };

        stack.Children.Add(
            new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.SemiBold
            });

        stack.Children.Add(
            new TextBlock
            {
                Text = description,
                FontSize = 12,
                Opacity = 0.72,
                TextWrapping = TextWrapping.Wrap
            });

        return new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Gray,
            CornerRadius = new CornerRadius(4),
            Child = stack
        };
    }

    private static Border CreateBusBlock(string text)
    {
        return new Border
        {
            Padding = new Thickness(8),
            Background = Brushes.Black,
            CornerRadius = new CornerRadius(2),
            Child = new TextBlock
            {
                Text = text,
                Foreground = Brushes.White,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };
    }

    private static Border CreateCircuitBlock(
        PrototypeCircuitBranch circuit)
    {
        var stack = new StackPanel
        {
            Spacing = 4
        };

        stack.Children.Add(
            new TextBlock
            {
                Text = $"{circuit.Code} · {circuit.Name}",
                FontWeight = FontWeight.SemiBold
            });

        stack.Children.Add(
            new TextBlock
            {
                Text = circuit.Breaker,
                FontSize = 12
            });

        stack.Children.Add(
            new TextBlock
            {
                Text = circuit.Differential,
                FontSize = 12
            });

        stack.Children.Add(
            new TextBlock
            {
                Text = circuit.Conductor,
                FontSize = 12,
                Opacity = 0.72
            });

        return new Border
        {
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.LightGray,
            CornerRadius = new CornerRadius(4),
            Child = stack
        };
    }

    private string GetParentLabel(PrototypeBlock block)
    {
        if (block.ParentUid is null)
        {
            return "—";
        }

        var parent = _state.GetBlock(block.ParentUid);

        return $"{parent.Code} / {block.FeederCode}";
    }

    private static string GetRoleText(
        PrototypeBlockKind kind)
    {
        return kind switch
        {
            PrototypeBlockKind.ServiceEntrance => "FUENTE / EMPALME",
            PrototypeBlockKind.MainBoard => "TABLERO PRINCIPAL",
            _ => "TABLERO DERIVADO"
        };
    }

    private void AddLog(string message)
    {
        var entry =
            $"{DateTime.Now:HH:mm:ss}  {message}";

        _events.Insert(0, entry);

        while (_events.Count > 30)
        {
            _events.RemoveAt(_events.Count - 1);
        }
    }

    private void ClearDrag()
    {
        _draggedUid = null;
        _dragMoved = false;
    }
}
