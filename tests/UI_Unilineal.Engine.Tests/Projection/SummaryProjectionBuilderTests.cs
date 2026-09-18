using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Projection;

public sealed class SummaryProjectionBuilderTests
{
    [Fact]
    public void Build_MinimalInput_ContainsSourceAndBoardButNoCircuitNode()
    {
        SingleLineInput input = SemanticFixtureFactory.Minimal();
        var index = new SemanticEntityIndex(input);

        SummaryProjection summary = new SummaryProjectionBuilder().Build(input, index, []);

        Assert.Equal(2, summary.Nodes.Count);
        Assert.Contains(summary.Nodes, x => x.Entity == new EntityReference(new EntityUid("S1"), EntityKind.Source));
        Assert.Contains(summary.Nodes, x => x.Entity == new EntityReference(new EntityUid("B1"), EntityKind.Board));
        Assert.DoesNotContain(summary.Nodes, x => x.Entity.Kind == EntityKind.Circuit);
        Assert.Single(summary.Connections);
        Assert.Equal(new EntityUid("SC1"), summary.Connections[0].SupplyConnectionUid);
        Assert.Equal(
            [new EntityReference(new EntityUid("S1"), EntityKind.Source)],
            summary.Roots);
        Assert.Equal(0, summary.Nodes.Single(x => x.Entity.Kind == EntityKind.Board).AlternateSupplyCount);
    }

    [Fact]
    public void Build_MultipleSourceSupplies_CountsAlternateAndKeepsBothRoots()
    {
        SingleLineInput input = MultipleSourceInput();
        var index = new SemanticEntityIndex(input);

        SummaryProjection summary = new SummaryProjectionBuilder().Build(input, index, []);

        SummaryNode board = summary.Nodes.Single(x => x.Entity.Kind == EntityKind.Board);

        Assert.Equal(1, board.AlternateSupplyCount);
        Assert.Equal(2, summary.Connections.Count);
        Assert.Equal(
            [
                new EntityReference(new EntityUid("S1"), EntityKind.Source),
                new EntityReference(new EntityUid("S2"), EntityKind.Source)
            ],
            summary.Roots);
        Assert.Equal(
            [new EntityUid("SC1"), new EntityUid("SC2")],
            summary.Connections.Select(x => x.SupplyConnectionUid).ToArray());
    }

    private static SingleLineInput MultipleSourceInput()
    {
        SingleLineInput minimal = SemanticFixtureFactory.Minimal();
        var emergency = new SourceInput(
            new EntityUid("S2"),
            "GEN",
            "Generador",
            SourceKind.Generator,
            "3F",
            400m,
            3,
            true,
            OperationalState.Active,
            DataState.Complete);
        var emergencySupply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(emergency.Uid, EntityKind.Source),
            null,
            minimal.Boards[0].Uid,
            SupplyRole.Emergency,
            1,
            false,
            OperationalState.Active,
            DataState.Complete);

        return new SingleLineInput(
            minimal.Project,
            [minimal.Sources[0], emergency],
            minimal.Boards,
            minimal.Buses,
            minimal.Circuits,
            [minimal.SupplyConnections[0], emergencySupply],
            minimal.Protections,
            minimal.Grounding,
            minimal.Results,
            minimal.Metadata);
    }
}
