using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Rendering.Avalonia.HitTesting;
using UI_Unilineal.Rendering.Avalonia.Interaction;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Rendering;

public sealed class SingleLineView : Control
{
    private const double ZoomStep = 1.2;

    private readonly ViewportController _viewportController = new();
    private readonly AvaloniaSceneRenderer _sceneRenderer = new();
    private readonly InteractionOverlayRenderer _overlayRenderer = new();
    private readonly HitTestPolicy _hitTestPolicy = new();
    private readonly InteractionInputTranslator _inputTranslator = new();

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
        ActualThemeVariantChanged +=
            (_, _) => RebuildResources();
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

            InteractionState previous =
                InteractionState;

            _inputTranslator.Cancel();
            _panning = false;
            ReleaseCapturedPointer();

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
            RaiseInteractionStateChanged(previous);
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
            RebuildResources();
        }
    }

    public InteractionMode InteractionMode
    {
        get => _inputTranslator.State.Mode;
        set
        {
            if (InteractionMode == value)
            {
                return;
            }

            InteractionState previous =
                InteractionState;

            _inputTranslator.SetMode(value);
            _panning = false;
            ReleaseCapturedPointer();
            ClearTransientOverlay();
            RaiseInteractionStateChanged(previous);
        }
    }

    public InteractionState InteractionState =>
        _inputTranslator.State;

    public ViewportState Viewport => _viewport;

    public InteractionOverlayState Overlay => _overlay;

    public event EventHandler<HitTestResult?>? PrimaryHitChanged;

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

    public event EventHandler<LayoutMoveRequestedEventArgs>? LayoutMoveRequested;

    public event EventHandler<ElectricalProposalRequestedEventArgs>?
        ElectricalProposalRequested;

    public event EventHandler<InteractionStateChangedEventArgs>?
        InteractionStateChanged;

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

        if (startPan)
        {
            InteractionState previous =
                InteractionState;

            _inputTranslator.BeginPan();

            if (InteractionState.Kind !=
                InteractionStateKind.Panning)
            {
                return;
            }

            _panning = true;
            _lastPanDip = point.Position;
            CapturePointer(e.Pointer);
            RaiseInteractionStateChanged(previous);
            e.Handled = true;
            return;
        }

        UpdatePrimaryHit(
            point.Position);

        if (_scene is null ||
            !point.Properties.IsLeftButtonPressed ||
            !HasUsableViewport())
        {
            return;
        }

        InteractionState beforeGesture =
            InteractionState;

        _inputTranslator.PointerPressed(
            _scene,
            _primaryHit,
            ViewportTransform.DipToSceneMm(
                point.Position,
                _viewport));

        if (IsPointerGestureState(
                InteractionState.Kind))
        {
            CapturePointer(e.Pointer);
            UpdateGestureOverlay();
            e.Handled = true;
        }

        RaiseInteractionStateChanged(
            beforeGesture);
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

        if (_scene is null ||
            !ReferenceEquals(
                _capturedPointer,
                e.Pointer) ||
            !IsPointerGestureState(
                InteractionState.Kind))
        {
            return;
        }

        InteractionState previous =
            InteractionState;

        _inputTranslator.PointerMoved(
            _scene,
            _primaryHit,
            ViewportTransform.DipToSceneMm(
                position,
                _viewport));

        UpdateGestureOverlay();
        RaiseInteractionStateChanged(previous);
        e.Handled = true;
    }

    protected override void OnPointerReleased(
        PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        Point position =
            e.GetPosition(this);

        if (_panning)
        {
            InteractionState previous =
                InteractionState;

            _panning = false;
            _inputTranslator.EndPan();
            ReleaseCapturedPointer(e.Pointer);
            RaiseInteractionStateChanged(previous);
            UpdatePrimaryHit(position);
            e.Handled = true;
            return;
        }

        UpdatePrimaryHit(position);

        if (_scene is null ||
            !ReferenceEquals(
                _capturedPointer,
                e.Pointer))
        {
            return;
        }

        InteractionState beforeRelease =
            InteractionState;

        InteractionTranslationResult result =
            _inputTranslator.PointerReleased(
                _scene,
                _primaryHit,
                ViewportTransform.DipToSceneMm(
                    position,
                    _viewport));

        ReleaseCapturedPointer(e.Pointer);
        ApplyTranslationResult(result);
        UpdateGestureOverlay();
        RaiseInteractionStateChanged(
            beforeRelease);
        e.Handled = true;
    }

    protected override void OnKeyDown(
        KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            InteractionState previous =
                InteractionState;
            InteractionTranslationResult cancelled =
                _inputTranslator.Cancel();

            if (cancelled.Cancelled)
            {
                _panning = false;
                ReleaseCapturedPointer();
                ClearTransientOverlay();
                RaiseInteractionStateChanged(previous);
                e.Handled = true;
            }

            return;
        }

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

    private void ApplyTranslationResult(
        InteractionTranslationResult result)
    {
        if (result.Selection is SelectionIntent selection)
        {
            _overlay =
                new InteractionOverlayState(
                    _overlay.Hovered,
                    new HashSet<SceneId>
                    {
                        selection.SceneElementId
                    });

            SelectionChanged?.Invoke(
                this,
                new SelectionChangedEventArgs(
                    selection));
        }

        if (result.LayoutMove is LayoutMoveIntent move)
        {
            LayoutMoveRequested?.Invoke(
                this,
                new LayoutMoveRequestedEventArgs(
                    move));
        }

        if (result.ElectricalConnection is
            ElectricalConnectionIntent electrical)
        {
            ElectricalProposalRequested?.Invoke(
                this,
                new ElectricalProposalRequestedEventArgs(
                    electrical));
        }
    }

    private void UpdateGestureOverlay()
    {
        MmRect? layoutGhost = null;
        MmRect? marquee = null;
        InteractionConnectionPreview? electrical = null;

        InteractionGestureState gesture =
            _inputTranslator.Gesture;

        switch (InteractionState.Kind)
        {
            case InteractionStateKind.DraggingLayout
                when gesture.SourceBounds is MmRect sourceBounds:
            {
                double dx =
                    gesture.CurrentScenePoint.X -
                    gesture.PressScenePoint.X;
                double dy =
                    gesture.CurrentScenePoint.Y -
                    gesture.PressScenePoint.Y;

                layoutGhost =
                    new MmRect(
                        sourceBounds.X + dx,
                        sourceBounds.Y + dy,
                        sourceBounds.Width,
                        sourceBounds.Height);
                break;
            }

            case InteractionStateKind.MarqueeSelecting:
                marquee =
                    RectFromPoints(
                        gesture.PressScenePoint,
                        gesture.CurrentScenePoint);
                break;

            case InteractionStateKind.ConnectingElectrical:
            case InteractionStateKind.CommandPreview:
                electrical =
                    new InteractionConnectionPreview(
                        gesture.PressScenePoint,
                        gesture.CurrentScenePoint);
                break;
        }

        _overlay =
            new InteractionOverlayState(
                _overlay.Hovered,
                _overlay.Selected,
                layoutGhost,
                marquee,
                electrical);

        InvalidateVisual();
    }

    private void ClearTransientOverlay()
    {
        _overlay =
            new InteractionOverlayState(
                _overlay.Hovered,
                _overlay.Selected);
        InvalidateVisual();
    }

    private void RebuildResources()
    {
        _resources =
            _drawingProfile is null
                ? null
                : new AvaloniaRenderResources(
                    _drawingProfile,
                    ResolveInteractiveTheme());

        InvalidateVisual();
    }

    private InteractiveThemeKind ResolveInteractiveTheme() =>
        ActualThemeVariant == ThemeVariant.Dark
            ? InteractiveThemeKind.Dark
            : InteractiveThemeKind.Light;

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
                    _overlay.Selected,
                    _overlay.LayoutGhostBounds,
                    _overlay.MarqueeBounds,
                    _overlay.ElectricalPreview);
            InvalidateVisual();
        }

        InteractionState previous =
            InteractionState;

        _inputTranslator.UpdateHover(primary);
        RaiseInteractionStateChanged(previous);
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

    private void CapturePointer(
        IPointer pointer)
    {
        _capturedPointer = pointer;
        pointer.Capture(this);
    }

    private void ReleaseCapturedPointer(
        IPointer? expected = null)
    {
        if (_capturedPointer is null ||
            (expected is not null &&
             !ReferenceEquals(
                 _capturedPointer,
                 expected)))
        {
            return;
        }

        _capturedPointer.Capture(null);
        _capturedPointer = null;
    }

    private void RaiseInteractionStateChanged(
        InteractionState previous)
    {
        InteractionState current =
            InteractionState;

        if (previous == current)
        {
            return;
        }

        InteractionStateChanged?.Invoke(
            this,
            new InteractionStateChangedEventArgs(
                previous,
                current));
    }

    private bool HasUsableViewport() =>
        _viewport.ViewportDip.Width > 0 &&
        _viewport.ViewportDip.Height > 0;

    private static bool IsPointerGestureState(
        InteractionStateKind kind) =>
        kind is
            InteractionStateKind.Selecting or
            InteractionStateKind.MarqueeSelecting or
            InteractionStateKind.DraggingLayout or
            InteractionStateKind.ConnectingElectrical;

    private static MmRect RectFromPoints(
        MmPoint first,
        MmPoint second)
    {
        double minX =
            Math.Min(
                first.X,
                second.X);
        double minY =
            Math.Min(
                first.Y,
                second.Y);
        double width =
            Math.Max(
                Math.Abs(
                    second.X - first.X),
                0.001);
        double height =
            Math.Max(
                Math.Abs(
                    second.Y - first.Y),
                0.001);

        return new MmRect(
            minX,
            minY,
            width,
            height);
    }

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
