using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Rendering.Avalonia.HitTesting;

namespace UI_Unilineal.Rendering.Avalonia.Interaction;

public sealed record SelectionIntent(
    SceneId SceneElementId,
    EntityReference Entity);

public sealed record LayoutMoveIntent(
    EntityUid EntityUid,
    MmPoint Position);

public sealed record ElectricalConnectionIntent(
    SceneId SourceSceneElementId,
    EntityReference SourceEntity,
    string SourceAnchorId,
    SceneId TargetSceneElementId,
    EntityReference TargetEntity,
    string TargetAnchorId);

public sealed record InteractionTranslationResult(
    InteractionState State,
    SelectionIntent? Selection = null,
    LayoutMoveIntent? LayoutMove = null,
    ElectricalConnectionIntent? ElectricalConnection = null,
    bool Cancelled = false);

public sealed class InteractionInputTranslator
{
    private readonly InteractionStateMachine _stateMachine = new();

    private InteractionGestureState _gesture =
        InteractionGestureState.Empty;

    public InteractionState State =>
        _stateMachine.State;

    public void SetMode(
        InteractionMode mode)
    {
        _stateMachine.SetMode(mode);
        _gesture = InteractionGestureState.Empty;
    }

    public InteractionTranslationResult PointerPressed(
        DiagramScene scene,
        HitTestResult? hit,
        MmPoint scenePoint)
    {
        ArgumentNullException.ThrowIfNull(scene);

        InteractionTransitionResult transition =
            State.Mode switch
            {
                InteractionMode.Navigate =>
                    BeginNavigate(
                        hit),
                InteractionMode.Layout =>
                    BeginLayout(
                        hit),
                InteractionMode.Electrical =>
                    BeginElectrical(
                        hit),
                _ => throw new InvalidOperationException(
                    $"Unsupported interaction mode '{State.Mode}'.")
            };

        if (!transition.Accepted)
        {
            _gesture = InteractionGestureState.Empty;
            return Current();
        }

        MmRect? sourceBounds =
            hit is null
                ? null
                : FindElement(
                    scene,
                    hit.SceneElementId)
                    ?.Bounds;

        _gesture =
            new InteractionGestureState(
                hit,
                scenePoint,
                scenePoint,
                sourceBounds);

        return Current();
    }

    public InteractionTranslationResult PointerMoved(
        DiagramScene scene,
        HitTestResult? hit,
        MmPoint scenePoint)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (State.Kind == InteractionStateKind.Idle ||
            State.Kind == InteractionStateKind.CommandPreview)
        {
            return Current();
        }

        _gesture =
            _gesture with
            {
                CurrentScenePoint = scenePoint
            };

        return Current();
    }

    public InteractionTranslationResult PointerReleased(
        DiagramScene scene,
        HitTestResult? hit,
        MmPoint scenePoint)
    {
        ArgumentNullException.ThrowIfNull(scene);

        InteractionTranslationResult result =
            State.Kind switch
            {
                InteractionStateKind.Selecting =>
                    CompleteSelection(),
                InteractionStateKind.MarqueeSelecting =>
                    CompleteMarquee(),
                InteractionStateKind.DraggingLayout =>
                    CompleteLayout(
                        scenePoint),
                InteractionStateKind.ConnectingElectrical =>
                    CompleteElectrical(
                        hit),
                _ =>
                    Current()
            };

        if (State.Kind != InteractionStateKind.CommandPreview)
        {
            _gesture = InteractionGestureState.Empty;
        }

        return result;
    }

    public InteractionTranslationResult Cancel()
    {
        if (State.Kind == InteractionStateKind.Idle)
        {
            return Current();
        }

        InteractionTransitionResult transition =
            _stateMachine.Apply(
                new InteractionEvent(
                    InteractionEventKind.Cancel));

        _gesture = InteractionGestureState.Empty;

        return new InteractionTranslationResult(
            State,
            Cancelled: transition.Accepted);
    }

    private InteractionTransitionResult BeginNavigate(
        HitTestResult? hit)
    {
        if (hit?.Entity is not null)
        {
            return _stateMachine.Apply(
                new InteractionEvent(
                    InteractionEventKind.BeginSelection,
                    hit.SceneElementId));
        }

        return _stateMachine.Apply(
            new InteractionEvent(
                InteractionEventKind.BeginMarquee));
    }

    private InteractionTransitionResult BeginLayout(
        HitTestResult? hit)
    {
        if (hit?.Entity is null)
        {
            return InteractionTransitionResult.Rejected(
                State,
                "Layout drag requires a semantic scene element.");
        }

        return _stateMachine.Apply(
            new InteractionEvent(
                InteractionEventKind.BeginLayoutDrag,
                hit.SceneElementId));
    }

    private InteractionTransitionResult BeginElectrical(
        HitTestResult? hit)
    {
        if (hit is null ||
            hit.Kind != HitKind.Anchor ||
            hit.Entity is null ||
            string.IsNullOrWhiteSpace(
                hit.AnchorId))
        {
            return InteractionTransitionResult.Rejected(
                State,
                "Electrical connection requires a semantic anchor.");
        }

        return _stateMachine.Apply(
            new InteractionEvent(
                InteractionEventKind.BeginElectricalConnection,
                hit.SceneElementId,
                hit.AnchorId));
    }

    private InteractionTranslationResult CompleteSelection()
    {
        HitTestResult? source =
            _gesture.PressHit;

        InteractionTransitionResult transition =
            _stateMachine.Apply(
                new InteractionEvent(
                    InteractionEventKind.EndSelection));

        SelectionIntent? selection =
            transition.Accepted &&
            source?.Entity is not null
                ? new SelectionIntent(
                    source.SceneElementId,
                    source.Entity)
                : null;

        return new InteractionTranslationResult(
            State,
            Selection: selection);
    }

    private InteractionTranslationResult CompleteMarquee()
    {
        _stateMachine.Apply(
            new InteractionEvent(
                InteractionEventKind.EndMarquee));

        return Current();
    }

    private InteractionTranslationResult CompleteLayout(
        MmPoint scenePoint)
    {
        HitTestResult? source =
            _gesture.PressHit;
        MmRect? sourceBounds =
            _gesture.SourceBounds;

        InteractionTransitionResult transition =
            _stateMachine.Apply(
                new InteractionEvent(
                    InteractionEventKind.EndLayoutDrag));

        LayoutMoveIntent? move =
            transition.Accepted &&
            source?.Entity is not null &&
            sourceBounds is not null
                ? new LayoutMoveIntent(
                    source.Entity.Uid,
                    new MmPoint(
                        sourceBounds.Value.X +
                        (scenePoint.X -
                         _gesture.PressScenePoint.X),
                        sourceBounds.Value.Y +
                        (scenePoint.Y -
                         _gesture.PressScenePoint.Y)))
                : null;

        return new InteractionTranslationResult(
            State,
            LayoutMove: move);
    }

    private InteractionTranslationResult CompleteElectrical(
        HitTestResult? target)
    {
        HitTestResult? source =
            _gesture.PressHit;

        if (source?.Entity is null ||
            string.IsNullOrWhiteSpace(
                source.AnchorId) ||
            target?.Entity is null ||
            target.Kind != HitKind.Anchor ||
            string.IsNullOrWhiteSpace(
                target.AnchorId))
        {
            _stateMachine.Apply(
                new InteractionEvent(
                    InteractionEventKind.Cancel));

            return Current();
        }

        InteractionTransitionResult transition =
            _stateMachine.Apply(
                new InteractionEvent(
                    InteractionEventKind.EndElectricalConnection,
                    target.SceneElementId,
                    target.AnchorId));

        ElectricalConnectionIntent? connection =
            transition.Accepted
                ? new ElectricalConnectionIntent(
                    source.SceneElementId,
                    source.Entity,
                    source.AnchorId!,
                    target.SceneElementId,
                    target.Entity,
                    target.AnchorId!)
                : null;

        return new InteractionTranslationResult(
            State,
            ElectricalConnection: connection);
    }

    private InteractionTranslationResult Current() =>
        new(State);

    private static SceneElement? FindElement(
        DiagramScene scene,
        SceneId id) =>
        scene.Elements
            .FirstOrDefault(
                element =>
                    element.Id == id);
}
