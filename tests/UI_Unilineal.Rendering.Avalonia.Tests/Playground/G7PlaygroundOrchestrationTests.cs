using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Interaction.Electrical;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Playground.ViewModels;
using UI_Unilineal.Rendering.Avalonia.Interaction;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Playground;

public sealed class G7PlaygroundOrchestrationTests
{
    [Fact]
    public void ReadOnlyCapabilities_KeepNavigationAndSelectionButRejectEditModes()
    {
        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                HostCapabilities.ReadOnly);

        SceneElement selectable =
            viewModel.Scene.Elements.First(
                element =>
                    element.SemanticReference is not null);

        viewModel.ApplySelection(
            new SelectionIntent(
                selectable.Id,
                selectable.SemanticReference!));

        Assert.Equal(
            selectable.SemanticReference,
            viewModel.SelectedEntity);
        Assert.Equal(
            InteractionMode.Navigate,
            viewModel.InteractionMode);
        Assert.False(
            viewModel.TrySetInteractionMode(
                InteractionMode.Layout));
        Assert.False(
            viewModel.TrySetInteractionMode(
                InteractionMode.Electrical));
        Assert.Equal(
            InteractionMode.Navigate,
            viewModel.InteractionMode);

        viewModel.OpenBoard(
            new EntityUid("B1"));

        Assert.Equal(
            DiagramSceneKind.BoardDetail,
            viewModel.Scene.Kind);
    }

    [Fact]
    public void LayoutMove_UsesLocalHistoryAndRebuildsImmutableScene()
    {
        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                EditableCapabilities());

        Assert.True(
            viewModel.TrySetInteractionMode(
                InteractionMode.Layout));

        SceneElement board =
            viewModel.Scene.Elements.First(
                element =>
                    element.SemanticReference?.Uid ==
                    new EntityUid("B1"));
        string baseline =
            DiagramSceneFingerprint.Compute(
                viewModel.Scene);
        var destination =
            new MmPoint(
                board.Bounds.X + 20,
                board.Bounds.Y + 10);

        Assert.True(
            viewModel.ApplyLayoutMove(
                new LayoutMoveIntent(
                    board.SemanticReference!.Uid,
                    destination)));
        string moved =
            DiagramSceneFingerprint.Compute(
                viewModel.Scene);

        Assert.NotEqual(
            baseline,
            moved);
        Assert.True(
            viewModel.CanUndoLayout);
        Assert.False(
            viewModel.CanRedoLayout);

        Assert.True(
            viewModel.UndoLayout());
        Assert.Equal(
            baseline,
            DiagramSceneFingerprint.Compute(
                viewModel.Scene));
        Assert.True(
            viewModel.CanRedoLayout);

        Assert.True(
            viewModel.RedoLayout());
        Assert.Equal(
            moved,
            DiagramSceneFingerprint.Compute(
                viewModel.Scene));
    }

    [Fact]
    public void ElectricalGesture_CreatesPreviewWithoutMutatingScene()
    {
        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                EditableCapabilities());

        Assert.True(
            viewModel.TrySetInteractionMode(
                InteractionMode.Electrical));

        ElectricalConnectionIntent intent =
            CompatibleConnectionIntent(
                viewModel.Scene);
        string baseline =
            DiagramSceneFingerprint.Compute(
                viewModel.Scene);

        Assert.True(
            viewModel.CreateElectricalProposal(
                intent));

        Assert.NotNull(
            viewModel.PendingElectricalProposal);
        Assert.Equal(
            baseline,
            DiagramSceneFingerprint.Compute(
                viewModel.Scene));

        viewModel.CancelElectricalProposal();

        Assert.Null(
            viewModel.PendingElectricalProposal);
        Assert.Equal(
            baseline,
            DiagramSceneFingerprint.Compute(
                viewModel.Scene));
    }

    private static ElectricalConnectionIntent CompatibleConnectionIntent(
        DiagramScene scene)
    {
        var endpoints =
            scene.Elements
                .Where(element =>
                    element.SemanticReference is not null)
                .SelectMany(
                    element =>
                        element.Anchors.Select(
                            anchor =>
                                new
                                {
                                    Element = element,
                                    Anchor = anchor
                                }))
                .ToArray();

        foreach (var source in endpoints)
        {
            foreach (var target in endpoints)
            {
                if (source.Element.Id ==
                        target.Element.Id ||
                    target.Element.SemanticReference?.Kind !=
                        EntityKind.Board ||
                    !AnchorCompatibility.CanConnect(
                        source.Anchor.Role,
                        target.Anchor.Role))
                {
                    continue;
                }

                return new ElectricalConnectionIntent(
                    source.Element.Id,
                    source.Element.SemanticReference!,
                    source.Anchor.Id,
                    target.Element.Id,
                    target.Element.SemanticReference!,
                    target.Anchor.Id);
            }
        }

        throw new InvalidOperationException(
            "The Playground scene has no compatible electrical anchor pair.");
    }

    private static HostCapabilities EditableCapabilities() =>
        new(
            CanEditLayout: true,
            CanEditElectrical: true,
            CanCreateCircuits: true,
            CanDeleteCircuits: true,
            CanEditProtection: true,
            CanExport: true,
            CanPersistLayout: true);
    [Theory]
    [InlineData(CommandResultStatus.Rejected)]
    [InlineData(CommandResultStatus.Conflict)]
    public async Task NonAppliedElectricalResult_DoesNotMutateDiagram(
        CommandResultStatus status)
    {
        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create(),
                EditableCapabilities(),
                new NonApplyingHandler(status));

        Assert.True(
            viewModel.TrySetInteractionMode(
                InteractionMode.Electrical));

        ElectricalConnectionIntent intent =
            CompatibleConnectionIntent(
                viewModel.Scene);
        string baseline =
            DiagramSceneFingerprint.Compute(
                viewModel.Scene);

        Assert.True(
            viewModel.CreateElectricalProposal(
                intent));
        ElectricalCommandProposal proposal =
            Assert.IsType<ElectricalCommandProposal>(
                viewModel.PendingElectricalProposal);

        CommandResult result =
            Assert.IsType<CommandResult>(
                await viewModel.ExecutePendingElectricalAsync(
                    confirmationGranted: true));

        Assert.Equal(status, result.Status);
        Assert.Equal(
            baseline,
            DiagramSceneFingerprint.Compute(
                viewModel.Scene));
        Assert.Same(
            proposal,
            viewModel.PendingElectricalProposal);
        Assert.Same(
            result,
            viewModel.LastElectricalResult);
    }

    private sealed class NonApplyingHandler :
        IElectricalCommandHandler
    {
        private readonly CommandResultStatus _status;

        public NonApplyingHandler(
            CommandResultStatus status)
        {
            _status = status;
        }

        public ValueTask<CommandResult> ExecuteAsync(
            ElectricalCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            CommandResult result =
                _status switch
                {
                    CommandResultStatus.Rejected =>
                        CommandResult.Rejected(
                            ["Rejected by G7 hardening fixture."]),
                    CommandResultStatus.Conflict =>
                        CommandResult.Conflict(
                            ["Revision conflict in G7 hardening fixture."]),
                    _ => throw new InvalidOperationException(
                        $"Unsupported non-applied status '{_status}'.")
                };

            return ValueTask.FromResult(result);
        }
    }

}
