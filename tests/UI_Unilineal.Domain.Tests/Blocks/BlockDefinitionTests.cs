using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Domain.Tests.Blocks;

public sealed class BlockDefinitionTests
{
    [Fact]
    public void BlockDefinition_PreservesPartsAndProvenance()
    {
        var part = new BlockPartDefinition(
            "BREAKER",
            "BREAKER",
            new MmPoint(5, 5),
            null);
        var block = new BlockDefinition(
            "MAIN_PROTECTION_BLOCK",
            "MainProtection",
            new MmSize(20, 20),
            [part],
            ["APP:BLOCK"]);

        Assert.Single(block.Parts);
        Assert.Equal("BREAKER", block.Parts[0].SymbolId);
        Assert.Equal(["APP:BLOCK"], block.ProvenanceIds);
    }

    [Fact]
    public void BlockDefinition_RejectsDuplicatePartIds()
    {
        var part = new BlockPartDefinition(
            "P1",
            "BREAKER",
            new MmPoint(0, 0),
            null);

        Assert.Throws<ArgumentException>(() => new BlockDefinition(
            "BLOCK",
            "Role",
            new MmSize(10, 10),
            [part, part],
            ["APP:BLOCK"]));
    }

    [Fact]
    public void BlockDefinition_DefensivelyCopiesParts()
    {
        var parts = new List<BlockPartDefinition>
        {
            new("P1", "BREAKER", new MmPoint(0, 0), null)
        };

        var block = new BlockDefinition(
            "BLOCK",
            "Role",
            new MmSize(10, 10),
            parts,
            ["APP:BLOCK"]);

        parts.Clear();

        Assert.Single(block.Parts);
    }
}
