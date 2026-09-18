using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Projection;

public sealed class BoardDetailProjectionBuilderTests
{
    [Fact]
    public void Build_FeederToDownstreamBoard_ProducesNavigableBranch()
    {
        SingleLineInput input = SemanticFixtureFactory.NestedBoards();
        BoardInput board = input.Boards.Single(x => x.Uid == new EntityUid("B1"));

        BoardDetailProjection detail = Build(board, input);

        BranchProjection branch = Assert.Single(detail.Branches);
        Assert.Equal(new EntityUid("C4"), branch.Circuit.Uid);
        Assert.Equal(BranchKind.DownstreamBoard, branch.Kind);
        Assert.Equal(
            new EntityReference(new EntityUid("B2"), EntityKind.Board),
            branch.Destination.Entity);
        Assert.Equal(
            new EntityReference(new EntityUid("B2"), EntityKind.Board),
            branch.Destination.NavigationTarget);
        Assert.Equal(
            [new EntityReference(new EntityUid("B2"), EntityKind.Board)],
            detail.NavigationTargets);
    }

    [Fact]
    public void Build_FinalCircuit_UsesCircuitNameWithoutNavigationTarget()
    {
        SingleLineInput input = SemanticFixtureFactory.NestedBoards();
        BoardInput board = input.Boards.Single(x => x.Uid == new EntityUid("B2"));

        BoardDetailProjection detail = Build(board, input);

        BranchProjection branch = Assert.Single(detail.Branches);
        Assert.Equal(BranchKind.FinalCircuit, branch.Kind);
        Assert.Equal(branch.Circuit.Name, branch.Destination.Label);
        Assert.Null(branch.Destination.NavigationTarget);
    }

    [Fact]
    public void Build_WithoutExplicitMainBus_SynthesizesDeterministicMainBus()
    {
        SingleLineInput input = SemanticFixtureFactory.Minimal();
        BoardInput board = input.Boards[0];

        BoardDetailProjection detail = Build(board, input);

        Assert.Equal(new EntityUid("BUS:B1:MAIN"), detail.MainBus.Bus.Uid);
        Assert.Equal(new EntityUid("B1"), detail.MainBus.Bus.BoardUid);
        Assert.Equal("MAIN", detail.MainBus.Bus.Code);
        Assert.Equal(BusRole.Main, detail.MainBus.Bus.Role);
        Assert.Empty(input.Buses);
    }

    [Fact]
    public void Build_AttachesProtectionChainAndCurrentResult()
    {
        SingleLineInput input = SemanticFixtureFactory.Minimal();
        BoardInput board = input.Boards[0];

        BoardDetailProjection detail = Build(board, input);

        BranchProjection branch = Assert.Single(detail.Branches);
        ProtectionInput protection = Assert.Single(branch.ProtectionChain);

        Assert.Equal(new EntityUid("PR1"), protection.Uid);
        Assert.NotNull(branch.Result);
        Assert.Equal(ResultState.Current, branch.Result!.ResultState);
        Assert.Equal(ProjectionStatus.Ok, branch.Status);
    }

    private static BoardDetailProjection Build(BoardInput board, SingleLineInput input)
    {
        var index = new SemanticEntityIndex(input);
        return new BoardDetailProjectionBuilder().Build(board, input, index, []);
    }
}
