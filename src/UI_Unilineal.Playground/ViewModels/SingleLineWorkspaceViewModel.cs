using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Interaction.Electrical;
using UI_Unilineal.Engine.Interaction.Layout;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Playground.Interaction;
using UI_Unilineal.Rendering.Avalonia.Interaction;

namespace UI_Unilineal.Playground.ViewModels;

public enum PlaygroundRouteKind
{
    ProjectSummary,
    BoardDetail
}

public sealed record PlaygroundRoute(
    PlaygroundRouteKind Kind,
    EntityUid? BoardUid = null);

public sealed class SingleLineWorkspaceViewModel
{
    private static readonly HostCapabilities DefaultCapabilities =
        new(
            CanEditLayout: true,
            CanEditElectrical: true,
            CanCreateCircuits: true,
            CanDeleteCircuits: true,
            CanEditProtection: true,
            CanExport: true,
            CanPersistLayout: true);

    private SingleLineInput _input;
    private SingleLineProjection _projection;
    private readonly SingleLineLayoutEngine _layoutEngine;
    private readonly IElectricalCommandHandler _electricalHandler;
    private readonly List<PlaygroundRoute> _history = [];
    private readonly Dictionary<PlaygroundRoute, LayoutCommandHistory>
        _layoutHistories = [];

    public SingleLineWorkspaceViewModel(
        PlaygroundFixture fixture)
        : this(
            fixture,
            DefaultCapabilities)
    {
    }

    public SingleLineWorkspaceViewModel(
        PlaygroundFixture fixture,
        HostCapabilities capabilities,
        IElectricalCommandHandler? electricalHandler = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(capabilities);

        _input = fixture.Input;
        _projection = fixture.Projection;
        Profile = fixture.Profile;
        Capabilities = capabilities;
        _layoutEngine =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics());
        _electricalHandler =
            electricalHandler ??
            new PlaygroundElectricalCommandHandler(
                _input);

        var initialRoute =
            new PlaygroundRoute(
                PlaygroundRouteKind.ProjectSummary);

        _history.Add(initialRoute);
        Scene = BuildScene(initialRoute);
    }

    public DiagramScene Scene { get; private set; }

    public RIC18DrawingProfile Profile { get; }

    public HostCapabilities Capabilities { get; }

    public PlaygroundRoute CurrentRoute =>
        _history[^1];

    public bool CanGoBack =>
        _history.Count > 1;

    public InteractionMode InteractionMode { get; private set; } =
        InteractionMode.Navigate;

    public EntityReference? SelectedEntity { get; private set; }

    public ElectricalCommandProposal? PendingElectricalProposal
    {
        get;
        private set;
    }

    public CommandResult? LastElectricalResult { get; private set; }

    public bool CanUndoLayout =>
        CurrentLayoutHistory()?.CanUndo == true;

    public bool CanRedoLayout =>
        CurrentLayoutHistory()?.CanRedo == true;

    public bool TrySetInteractionMode(
        InteractionMode mode)
    {
        bool allowed =
            mode switch
            {
                InteractionMode.Navigate => true,
                InteractionMode.Layout =>
                    Capabilities.CanEditLayout,
                InteractionMode.Electrical =>
                    Capabilities.CanEditElectrical,
                _ => false
            };

        if (!allowed)
        {
            return false;
        }

        InteractionMode = mode;

        if (mode != InteractionMode.Electrical)
        {
            CancelElectricalProposal();
        }

        return true;
    }

    public void ApplySelection(
        SelectionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        SelectedEntity = intent.Entity;
    }

    public void ShowSummary() =>
        Navigate(
            new PlaygroundRoute(
                PlaygroundRouteKind.ProjectSummary));

    public void OpenBoard(
        EntityUid boardUid)
    {
        ArgumentNullException.ThrowIfNull(boardUid);

        if (!_projection.BoardDetails.Any(
                detail => detail.Board.Uid == boardUid))
        {
            throw new ArgumentException(
                $"Board '{boardUid.Value}' does not exist in the Playground projection.",
                nameof(boardUid));
        }

        Navigate(
            new PlaygroundRoute(
                PlaygroundRouteKind.BoardDetail,
                boardUid));
    }

    public void Back()
    {
        if (!CanGoBack)
        {
            return;
        }

        PlaygroundRoute target =
            _history[^2];
        DiagramScene scene =
            BuildScene(target);

        _history.RemoveAt(
            _history.Count - 1);
        Scene = scene;
        ResetRouteTransientState();
    }

    public bool ApplyLayoutMove(
        LayoutMoveIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!Capabilities.CanEditLayout ||
            InteractionMode != InteractionMode.Layout)
        {
            return false;
        }

        LayoutCommandHistory history =
            GetOrCreateLayoutHistory(
                CurrentRoute);
        LayoutCommandResult result =
            history.Execute(
                new MoveEntityCommand(
                    intent.EntityUid,
                    intent.Position));

        if (!result.Changed)
        {
            return false;
        }

        Scene =
            BuildScene(
                CurrentRoute);
        return true;
    }

    public bool SetSelectedLayoutLockMode(
        LayoutLockMode lockMode)
    {
        if (!Capabilities.CanEditLayout ||
            InteractionMode != InteractionMode.Layout ||
            SelectedEntity is null)
        {
            return false;
        }

        LayoutCommandHistory history =
            GetOrCreateLayoutHistory(
                CurrentRoute);
        LayoutCommandResult result =
            history.Execute(
                new SetEntityLockModeCommand(
                    SelectedEntity.Uid,
                    lockMode));

        if (!result.Changed)
        {
            return false;
        }

        Scene =
            BuildScene(
                CurrentRoute);
        return true;
    }

    public bool ResetSelectedLayout()
    {
        if (!Capabilities.CanEditLayout ||
            InteractionMode != InteractionMode.Layout ||
            SelectedEntity is null)
        {
            return false;
        }

        LayoutCommandHistory history =
            GetOrCreateLayoutHistory(
                CurrentRoute);
        LayoutCommandResult result =
            history.Execute(
                new ResetEntityPositionCommand(
                    SelectedEntity.Uid));

        if (!result.Changed)
        {
            return false;
        }

        Scene =
            BuildScene(
                CurrentRoute);
        return true;
    }

    public bool UndoLayout()
    {
        LayoutCommandHistory? history =
            CurrentLayoutHistory();

        if (!Capabilities.CanEditLayout ||
            history?.CanUndo != true)
        {
            return false;
        }

        history.Undo();
        Scene =
            BuildScene(
                CurrentRoute);
        return true;
    }

    public bool RedoLayout()
    {
        LayoutCommandHistory? history =
            CurrentLayoutHistory();

        if (!Capabilities.CanEditLayout ||
            history?.CanRedo != true)
        {
            return false;
        }

        history.Redo();
        Scene =
            BuildScene(
                CurrentRoute);
        return true;
    }

    public bool CreateElectricalProposal(
        ElectricalConnectionIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (!Capabilities.CanEditElectrical ||
            InteractionMode != InteractionMode.Electrical)
        {
            return false;
        }

        ElectricalAnchorEndpoint? source =
            ResolveEndpoint(
                intent.SourceSceneElementId,
                intent.SourceEntity,
                intent.SourceAnchorId);
        ElectricalAnchorEndpoint? target =
            ResolveEndpoint(
                intent.TargetSceneElementId,
                intent.TargetEntity,
                intent.TargetAnchorId);

        if (source is null ||
            target is null ||
            target.Entity.Kind != EntityKind.Board ||
            !AnchorCompatibility.CanConnect(
                source.Role,
                target.Role))
        {
            return false;
        }

        CreateSupplyConnectionCommand? command =
            CreateSupplyConnectionCommandFor(
                source,
                target);

        if (command is null)
        {
            return false;
        }

        PendingElectricalProposal =
            ElectricalCommandProposalFactory.Create(
                command,
                SingleLineInputFingerprint.Compute(
                    _input),
                source,
                target);
        LastElectricalResult = null;

        return true;
    }

    public void CancelElectricalProposal()
    {
        PendingElectricalProposal = null;
    }

    public async ValueTask<CommandResult?> ExecutePendingElectricalAsync(
        bool confirmationGranted,
        CancellationToken cancellationToken = default)
    {
        ElectricalCommandProposal? proposal =
            PendingElectricalProposal;

        if (!Capabilities.CanEditElectrical ||
            proposal is null)
        {
            return null;
        }

        var request =
            new ElectricalCommandRequest(
                proposal.Command,
                proposal.ExpectedRevision,
                confirmationGranted);

        CommandResult result =
            await _electricalHandler.ExecuteAsync(
                request,
                cancellationToken);

        LastElectricalResult = result;

        if (result.Status != CommandResultStatus.Applied)
        {
            return result;
        }

        SingleLineInput appliedInput =
            result.NewInput ??
            throw new InvalidOperationException(
                "An applied electrical command must provide a new input snapshot.");

        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(
                appliedInput);

        if (!projectionResult.Success ||
            projectionResult.Projection is null)
        {
            throw new InvalidOperationException(
                "The Playground electrical handler returned an applied input that cannot be projected.");
        }

        _input = appliedInput;
        _projection = projectionResult.Projection;
        _layoutHistories.Clear();
        PendingElectricalProposal = null;
        SelectedEntity = null;
        Scene =
            BuildScene(
                CurrentRoute);

        return result;
    }

    private void Navigate(
        PlaygroundRoute route)
    {
        if (route == CurrentRoute)
        {
            return;
        }

        DiagramScene scene =
            BuildScene(route);

        _history.Add(route);
        Scene = scene;
        ResetRouteTransientState();
    }

    private void ResetRouteTransientState()
    {
        SelectedEntity = null;
        PendingElectricalProposal = null;
        LastElectricalResult = null;
    }

    private DiagramScene BuildScene(
        PlaygroundRoute route)
    {
        DiagramLayoutState? layoutState =
            _layoutHistories.TryGetValue(
                route,
                out LayoutCommandHistory? history)
                ? history.CurrentState
                : null;

        SingleLineLayoutResult result =
            route.Kind switch
            {
                PlaygroundRouteKind.ProjectSummary =>
                    _layoutEngine.LayoutSummary(
                        _projection,
                        Profile,
                        layoutState),
                PlaygroundRouteKind.BoardDetail
                    when route.BoardUid is not null =>
                    _layoutEngine.LayoutBoardDetail(
                        _projection,
                        route.BoardUid,
                        Profile,
                        layoutState),
                _ => throw new InvalidOperationException(
                    "The Playground route is incomplete.")
            };

        if (!result.Success ||
            result.Scene is null)
        {
            string detail =
                result.Failure?.Message ??
                "The layout engine returned no scene.";

            throw new InvalidOperationException(
                $"Could not build Playground scene: {detail}");
        }

        return result.Scene;
    }

    private LayoutCommandHistory GetOrCreateLayoutHistory(
        PlaygroundRoute route)
    {
        if (_layoutHistories.TryGetValue(
                route,
                out LayoutCommandHistory? existing))
        {
            return existing;
        }

        EntityUid scopeUid =
            route.Kind switch
            {
                PlaygroundRouteKind.ProjectSummary =>
                    _projection.ProjectUid,
                PlaygroundRouteKind.BoardDetail
                    when route.BoardUid is not null =>
                    route.BoardUid,
                _ => throw new InvalidOperationException(
                    "The Playground route is incomplete.")
            };

        var state =
            new DiagramLayoutState(
                route.Kind == PlaygroundRouteKind.ProjectSummary
                    ? DiagramSceneKind.ProjectSummary
                    : DiagramSceneKind.BoardDetail,
                scopeUid,
                "G7-PLAYGROUND-1",
                [],
                null);
        var history =
            new LayoutCommandHistory(
                state);

        _layoutHistories.Add(
            route,
            history);
        return history;
    }

    private LayoutCommandHistory? CurrentLayoutHistory() =>
        _layoutHistories.TryGetValue(
            CurrentRoute,
            out LayoutCommandHistory? history)
            ? history
            : null;

    private ElectricalAnchorEndpoint? ResolveEndpoint(
        SceneId sceneElementId,
        EntityReference entity,
        string anchorId)
    {
        SceneElement? element =
            Scene.Elements.SingleOrDefault(
                candidate =>
                    candidate.Id == sceneElementId);

        if (element?.SemanticReference != entity)
        {
            return null;
        }

        SceneAnchor? anchor =
            element.Anchors.SingleOrDefault(
                candidate =>
                    string.Equals(
                        candidate.Id,
                        anchorId,
                        StringComparison.Ordinal));

        return anchor is null
            ? null
            : new ElectricalAnchorEndpoint(
                entity,
                anchor.Id,
                anchor.Role);
    }

    private CreateSupplyConnectionCommand? CreateSupplyConnectionCommandFor(
        ElectricalAnchorEndpoint source,
        ElectricalAnchorEndpoint target)
    {
        EntityReference origin;
        EntityUid? throughCircuitUid;

        switch (source.Entity.Kind)
        {
            case EntityKind.Source:
                origin = source.Entity;
                throughCircuitUid = null;
                break;

            case EntityKind.Circuit:
            {
                CircuitInput? circuit =
                    _input.Circuits.SingleOrDefault(
                        candidate =>
                            candidate.Uid ==
                            source.Entity.Uid);

                if (circuit is null)
                {
                    return null;
                }

                origin =
                    new EntityReference(
                        circuit.BoardUid,
                        EntityKind.Board);
                throughCircuitUid =
                    circuit.Uid;
                break;
            }

            case EntityKind.Board:
            {
                CircuitInput? feeder =
                    _input.Circuits
                        .Where(candidate =>
                            candidate.BoardUid ==
                            source.Entity.Uid &&
                            candidate.Role ==
                            CircuitRole.Feeder)
                        .OrderBy(
                            candidate => candidate.Number)
                        .ThenBy(
                            candidate => candidate.Uid.Value,
                            StringComparer.Ordinal)
                        .FirstOrDefault();

                origin = source.Entity;
                throughCircuitUid =
                    feeder?.Uid;
                break;
            }

            default:
                return null;
        }

        string uidValue =
            string.Join(
                "-",
                "PLAYGROUND",
                "SC",
                source.Entity.Uid.Value,
                target.Entity.Uid.Value);

        var connection =
            new SupplyConnection(
                new EntityUid(uidValue),
                origin,
                throughCircuitUid,
                target.Entity.Uid,
                SupplyRole.Normal,
                0,
                true,
                OperationalState.Active,
                DataState.Complete);

        return new CreateSupplyConnectionCommand(
            connection);
    }
}
