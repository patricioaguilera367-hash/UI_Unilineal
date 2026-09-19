using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Rendering.Avalonia.HitTesting;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed class SingleLineView : Control
{
    private const double ZoomStep = 1.2;

    private readonly ViewportController _viewportController = new();
    private readonly AvaloniaSceneRenderer _sceneRenderer = new();
    private readonly InteractionOverlayRenderer _overlayRenderer = new();
    private readonly HitTestPolicy _hitTestPolicy = new();

    private DiagramScene? _scene;
    private RIC18DrawingProfile? _drawingProfile;
    private SceneSpatialIndex? _spatialIndex;
    private HitTestIndex? _hitTestIndex;
    private AvaloniaRenderResources? _resources;
    private ViewportState _viewport =
        new(
            zoom: 1,
            panDip: default,
            viewportDip: default);
    private InteractionOverlayState _overlay =
        InteractionOverlayState.Empty;
    private HitTestResult? _primaryHit;
    private bool _spacePressed;
    private bool _panning;
    private Point _lastPanDip;
    private IPointer? _capturedPointer;

    public SingleLineView()
    {
        Focusable = true;
        ClipToBounds = true;
    }

    public DiagramScene? Scene
    {
        get => _scene;
        set
        {
            if (ReferenceEquals(
                    _scene,
                    value))
            {
                return;
            }

            _scene = value;
            _spatialIndex =
                value is null
                    ? null
                    : new SceneSpatialIndex(value);
            _hitTestIndex =
                value is null || _spatialIndex is null
                    ? null
                    : new HitTestIndex(
                        value,
                        _spatialIndex);
            _overlay =
                InteractionOverlayState.Empty;
            SetPrimaryHit(null);
            InvalidateVisual();
        }
    }

    public RIC18DrawingProfile? DrawingProfile
    {
        get => _drawingProfile;
        set
        {
            if (ReferenceEquals(
                    _drawingProfile,
                    value))
            {
                return;
            }

            _drawingProfile = value;
            _resources =
                value is null
                    ? null
                    : new AvaloniaRenderResources(
                        value,
                        InteractiveThemeKind.Light);
            InvalidateVisual();
        }
    }

    public ViewportState Viewport => _viewport;

    public InteractionOverlayState Overlay => _overlay;

    public event EventHandler<HitTestResult?>? PrimaryHitChanged;

    public void FitScene()
    {
        if (_scene is null ||
            !HasUsableViewport())
        {
            return;
        }

        SetViewport(
            _viewportController.FitScene(
                _viewport,
                _scene.Bounds));
    }

    public void FitSelection()
    {
        if (_scene is null ||
            _overlay.Selected.Count == 0 ||
            !HasUsableViewport())
        {
            return;
        }

        SceneElement[] selected =
            _scene.Elements
                .Where(element =>
                    _overlay.Selected.Contains(
                        element.Id))
                .ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        SetViewport(
            _viewportController.FitSelection(
                _viewport,
                BoundsOf(selected)));
    }

    public void ZoomIn() =>
        ZoomAtViewportCenter(ZoomStep);

    public void ZoomOut() =>
        ZoomAtViewportCenter(1.0 / ZoomStep);

    public void ActualSize()
    {
        if (!HasUsableViewport())
        {
            return;
        }

        SetViewport(
            _viewportController.ActualSize(
                _viewport));
    }

    public void CenterOn(
        MmPoint point)
    {
        if (!HasUsableViewport())
        {
            return;
        }

        SetViewport(
            _viewportController.CenterOn(
                _viewport,
                point));
    }

    public override void Render(
        DrawingContext context)
    {
        base.Render(context);

        context.FillRectangle(
            Brushes.Transparent,
            new Rect(Bounds.Size));

        if (_scene is null ||
            _drawingProfile is null ||
            _spatialIndex is null ||
            _resources is null ||
            !HasUsableViewport())
        {
            return;
        }

        _sceneRenderer.Render(
            context,
            _scene,
            _drawingProfile,
            _viewport,
            _spatialIndex,
            _resources);

        _overlayRenderer.Render(
            context,
            _scene,
            _viewport,
            _overlay,
            _resources);
    }

    protected override void OnSizeChanged(
        SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        _viewport =
            new ViewportState(
                _viewport.Zoom,
                _viewport.PanDip,
                e.NewSize,
                _viewport.MinZoom,
                _viewport.MaxZoom);
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(
        PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        if (!HasUsableViewport() ||
            Math.Abs(e.Delta.Y) <= double.Epsilon)
        {
            return;
        }

        double factor =
            Math.Pow(
                ZoomStep,
                e.Delta.Y);
        SetViewport(
            _viewportController.ZoomAt(
                _viewport,
                e.GetPosition(this),
                factor));
        UpdatePrimaryHit(
            e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerPressed(
        PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        PointerPoint point =
            e.GetCurrentPoint(this);
        bool startPan =
            point.Properties.IsMiddleButtonPressed ||
            (_spacePressed &&
             point.Properties.IsLeftButtonPressed);

        if (!startPan)
        {
            UpdatePrimaryHit(
                point.Position);
            return;
        }

        _panning = true;
        _lastPanDip = point.Position;
        _capturedPointer = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(
        PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        Point position =
            e.GetPosition(this);

        if (_panning)
        {
            Vector delta =
                position - _lastPanDip;
            _lastPanDip = position;
            SetViewport(
                _viewportController.Pan(
                    _viewport,
                    delta));
            e.Handled = true;
            return;
        }

        UpdatePrimaryHit(position);
    }

    protected override void OnPointerReleased(
        PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_panning)
        {
            UpdatePrimaryHit(
                e.GetPosition(this));
            return;
        }

        _panning = false;
        if (ReferenceEquals(
                _capturedPointer,
                e.Pointer))
        {
            e.Pointer.Capture(null);
        }

        _capturedPointer = null;
        UpdatePrimaryHit(
            e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnKeyDown(
        KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Space)
        {
            _spacePressed = true;
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Add:
            case Key.OemPlus:
                ZoomIn();
                e.Handled = true;
                break;

            case Key.Subtract:
            case Key.OemMinus:
                ZoomOut();
                e.Handled = true;
                break;

            case Key.F:
                if ((e.KeyModifiers &
                     KeyModifiers.Shift) != 0)
                {
                    FitSelection();
                }
                else
                {
                    FitScene();
                }

                e.Handled = true;
                break;

            case Key.Home:
                FitScene();
                e.Handled = true;
                break;
        }
    }

    protected override void OnKeyUp(
        KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (e.Key != Key.Space)
        {
            return;
        }

        _spacePressed = false;
        e.Handled = true;
    }

    private void ZoomAtViewportCenter(
        double factor)
    {
        if (!HasUsableViewport())
        {
            return;
        }

        SetViewport(
            _viewportController.ZoomAt(
                _viewport,
                new Point(
                    _viewport.ViewportDip.Width / 2.0,
                    _viewport.ViewportDip.Height / 2.0),
                factor));
    }

    private void SetViewport(
        ViewportState state)
    {
        if (_viewport == state)
        {
            return;
        }

        _viewport = state;
        InvalidateVisual();
    }

    private void UpdatePrimaryHit(
        Point pointerDip)
    {
        HitTestResult? primary =
            _hitTestIndex?
                .HitTest(
                    pointerDip,
                    _viewport,
                    _hitTestPolicy)
                .FirstOrDefault();

        SceneId? hovered =
            primary?.SceneElementId;

        if (_overlay.Hovered != hovered)
        {
            _overlay =
                new InteractionOverlayState(
                    hovered,
                    _overlay.Selected);
            InvalidateVisual();
        }

        SetPrimaryHit(primary);
    }

    private void SetPrimaryHit(
        HitTestResult? hit)
    {
        if (Equals(
                _primaryHit,
                hit))
        {
            return;
        }

        _primaryHit = hit;
        PrimaryHitChanged?.Invoke(
            this,
            hit);
    }

    private bool HasUsableViewport() =>
        _viewport.ViewportDip.Width > 0 &&
        _viewport.ViewportDip.Height > 0;

    private static MmRect BoundsOf(
        IReadOnlyList<SceneElement> elements)
    {
        double minX =
            elements.Min(element => element.Bounds.X);
        double minY =
            elements.Min(element => element.Bounds.Y);
        double maxX =
            elements.Max(element => element.Bounds.Right);
        double maxY =
            elements.Max(element => element.Bounds.Bottom);

        return new MmRect(
            minX,
            minY,
            Math.Max(
                maxX - minX,
                0.001),
            Math.Max(
                maxY - minY,
                0.001));
    }
}
