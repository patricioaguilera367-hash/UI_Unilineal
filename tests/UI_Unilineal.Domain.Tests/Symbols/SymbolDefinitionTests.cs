using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Domain.Tests.Symbols;

public sealed class SymbolDefinitionTests
{
    [Fact]
    public void SymbolDefinition_ExposesPowerAnchorsAndVectorPrimitives()
    {
        SymbolDefinition symbol = Breaker();

        Assert.Contains(symbol.Anchors, x => x.Role == AnchorRole.PowerIn);
        Assert.Contains(symbol.Anchors, x => x.Role == AnchorRole.PowerOut);
        Assert.Contains(symbol.Primitives, x => x is LineSymbolPrimitive);
        Assert.Single(symbol.LabelSlots);
    }

    [Fact]
    public void SymbolDefinition_RejectsDuplicateAnchorIds()
    {
        var anchor = new AnchorDefinition(
            "POWER",
            AnchorRole.PowerIn,
            new MmPoint(0, 5),
            AnchorDirection.Left);

        Assert.Throws<ArgumentException>(() => new SymbolDefinition(
            "BREAKER",
            "Breaker",
            new MmRect(0, 0, 10, 10),
            [anchor, anchor],
            [],
            [],
            ["APP:BASE"]));
    }

    [Fact]
    public void SymbolDefinition_DefensivelyCopiesCollections()
    {
        var primitives = new List<SymbolPrimitive>
        {
            new LineSymbolPrimitive(
                new MmPoint(0, 0),
                new MmPoint(10, 10),
                "POWER")
        };

        SymbolDefinition symbol = new(
            "BREAKER",
            "Breaker",
            new MmRect(0, 0, 10, 10),
            [],
            primitives,
            [],
            ["APP:BASE"]);

        primitives.Clear();

        Assert.Single(symbol.Primitives);
    }

    private static SymbolDefinition Breaker() =>
        new(
            "BREAKER",
            "Breaker",
            new MmRect(0, 0, 10, 10),
            [
                new AnchorDefinition(
                    "IN",
                    AnchorRole.PowerIn,
                    new MmPoint(0, 5),
                    AnchorDirection.Left),
                new AnchorDefinition(
                    "OUT",
                    AnchorRole.PowerOut,
                    new MmPoint(10, 5),
                    AnchorDirection.Right)
            ],
            [
                new LineSymbolPrimitive(
                    new MmPoint(0, 5),
                    new MmPoint(10, 5),
                    "POWER")
            ],
            [
                new LabelSlot(
                    "RATING",
                    new MmRect(0, 0, 10, 3),
                    0,
                    true,
                    "TECH")
            ],
            ["APP:BASE"]);
}
