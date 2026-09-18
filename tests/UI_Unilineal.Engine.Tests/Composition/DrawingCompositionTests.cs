using System.Reflection;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Composition;

public sealed class DrawingCompositionTests
{
    [Fact]
    public void CompositionIdFactory_IdenticalSemanticScope_ProducesStableIds()
    {
        var board = new EntityUid("B1");
        var circuit = new EntityUid("C1");
        var protection = new EntityUid("PR1");

        Assert.Equal(
            "summary/board/B1",
            CompositionIdFactory.SummaryEntity(
                new EntityReference(board, EntityKind.Board)));
        Assert.Equal(
            "detail/B1/branch/C1",
            CompositionIdFactory.DetailBranch(board, circuit));
        Assert.Equal(
            "detail/B1/branch/C1/protection/PR1",
            CompositionIdFactory.DetailProtection(board, circuit, protection));
        Assert.Equal(
            CompositionIdFactory.DetailProtection(board, circuit, protection),
            CompositionIdFactory.DetailProtection(board, circuit, protection));
    }

    [Fact]
    public void DrawingComposition_PreservesSemanticBlocksAndAnchorConnections()
    {
        var block = new CompositionBlock(
            "summary/board/B1",
            "BOARD_SUMMARY_BLOCK",
            "BoardSummary",
            new EntityReference(new EntityUid("B1"), EntityKind.Board),
            new Dictionary<string, string>
            {
                ["CODE"] = "TGBT"
            },
            ProjectionStatus.Ok,
            null);
        var connection = new CompositionConnection(
            "summary/supply/SC1",
            new CompositionAnchorRef(
                "summary/source/S1",
                AnchorRole.PowerOut,
                "OUT"),
            new CompositionAnchorRef(
                block.Id,
                AnchorRole.PowerIn,
                "IN"),
            "POWER",
            new EntityReference(
                new EntityUid("SC1"),
                EntityKind.SupplyConnection));

        var composition = new DrawingComposition(
            DrawingCompositionKind.Summary,
            new EntityReference(new EntityUid("P1"), EntityKind.Project),
            [block],
            [connection],
            "INPUT-FP",
            "PROFILE-FP");

        Assert.Single(composition.Blocks);
        Assert.Single(composition.Connections);
        Assert.Equal(AnchorRole.PowerOut, composition.Connections[0].Source.Role);
        Assert.Equal("TGBT", composition.Blocks[0].Labels["CODE"]);
    }

    [Fact]
    public void CompositionContracts_DoNotContainGeometryAvaloniaOrElectricalInputObjects()
    {
        Type[] contractTypes =
        [
            typeof(DrawingComposition),
            typeof(CompositionBlock),
            typeof(CompositionConnection),
            typeof(CompositionAnchorRef)
        ];

        string[] forbiddenTypeNames =
        [
            "MmPoint",
            "MmRect",
            "MmSize",
            "Avalonia",
            nameof(BoardInput),
            nameof(CircuitInput),
            nameof(ProtectionInput),
            nameof(SupplyConnection)
        ];

        foreach (Type type in contractTypes)
        {
            PropertyInfo[] properties = type.GetProperties();

            foreach (PropertyInfo property in properties)
            {
                string signature = property.PropertyType.FullName ??
                    property.PropertyType.Name;

                Assert.DoesNotContain(
                    forbiddenTypeNames,
                    forbidden => signature.Contains(
                        forbidden,
                        StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void DrawingComposition_DefensivelyCopiesBlocksAndLabels()
    {
        var labels = new Dictionary<string, string>
        {
            ["CODE"] = "TGBT"
        };
        var block = new CompositionBlock(
            "summary/board/B1",
            "BOARD_SUMMARY_BLOCK",
            "BoardSummary",
            null,
            labels,
            ProjectionStatus.Ok,
            null);
        var blocks = new List<CompositionBlock> { block };

        var composition = new DrawingComposition(
            DrawingCompositionKind.Summary,
            null,
            blocks,
            [],
            "INPUT",
            "PROFILE");

        labels["CODE"] = "CHANGED";
        blocks.Clear();

        Assert.Single(composition.Blocks);
        Assert.Equal("TGBT", composition.Blocks[0].Labels["CODE"]);
    }
}
