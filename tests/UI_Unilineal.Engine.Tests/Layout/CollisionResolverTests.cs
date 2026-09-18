using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class CollisionResolverTests
{
    [Fact]
    public void StrictValidation_OverlappingStructuralGroups_AreErrors()
    {
        var first = Group(
            "scene/block/A",
            new MmRect(0, 0, 20, 20));
        var second = Group(
            "scene/block/B",
            new MmRect(10, 10, 20, 20));
        var scene = new DiagramScene(
            new MmRect(0, 0, 100, 100),
            [first, second],
            Metadata());

        DiagramSceneValidationResult result =
            new DiagramSceneValidator().Validate(
                scene,
                SceneValidationMode.Strict);

        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                SceneValidationCodes.StructuralBlockOverlap);
    }

    [Fact]
    public void Resolve_MovesAutoBlocksToGridWithoutMovingLockedBlocks()
    {
        RIC18DrawingProfile profile = Profile();
        var locked = new PositionedCompositionBlock(
            "A",
            new MmRect(1.3, 1.7, 20, 20));
        var autoB = new PositionedCompositionBlock(
            "B",
            new MmRect(10.2, 10.1, 20, 20));
        var autoC = new PositionedCompositionBlock(
            "C",
            new MmRect(12.1, 12.2, 20, 20));
        var layout = new PositionedLayout(
            [locked, autoB, autoC],
            new MmRect(0, 0, 100, 100));

        PositionedLayout resolved =
            new CollisionResolver().Resolve(
                layout,
                profile.Layout,
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "A"
                });

        Assert.Equal(
            locked.Bounds,
            resolved.GetBlock("A").Bounds);

        foreach (string id in new[] { "B", "C" })
        {
            MmRect bounds = resolved.GetBlock(id).Bounds;
            Assert.True(IsOnGrid(bounds.X, profile.Layout.GridMm));
            Assert.True(IsOnGrid(bounds.Y, profile.Layout.GridMm));
        }

        AssertNoOverlaps(resolved.Blocks);
        Assert.True(
            resolved.GetBlock("B").Bounds.Y <=
            resolved.GetBlock("C").Bounds.Y);
    }

    [Fact]
    public void Resolve_LockedOverlap_IsPreservedForStrictValidatorToReject()
    {
        RIC18DrawingProfile profile = Profile();
        var first = new PositionedCompositionBlock(
            "A",
            new MmRect(0, 0, 20, 20));
        var second = new PositionedCompositionBlock(
            "B",
            new MmRect(10, 10, 20, 20));
        var layout = new PositionedLayout(
            [first, second],
            new MmRect(0, 0, 50, 50));

        PositionedLayout resolved =
            new CollisionResolver().Resolve(
                layout,
                profile.Layout,
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "A",
                    "B"
                });

        Assert.Equal(first.Bounds, resolved.GetBlock("A").Bounds);
        Assert.Equal(second.Bounds, resolved.GetBlock("B").Bounds);
        Assert.True(Overlaps(
            resolved.GetBlock("A").Bounds,
            resolved.GetBlock("B").Bounds));
    }

    [Fact]
    public void Resolve_IsDeterministicAcrossInputOrder()
    {
        RIC18DrawingProfile profile = Profile();
        PositionedCompositionBlock[] blocks =
        [
            new("A", new MmRect(0, 0, 20, 20)),
            new("B", new MmRect(10, 10, 20, 20)),
            new("C", new MmRect(12, 12, 20, 20))
        ];

        PositionedLayout first = new CollisionResolver().Resolve(
            new PositionedLayout(
                blocks,
                new MmRect(0, 0, 100, 100)),
            profile.Layout);
        PositionedLayout second = new CollisionResolver().Resolve(
            new PositionedLayout(
                blocks.Reverse(),
                new MmRect(0, 0, 100, 100)),
            profile.Layout);

        Assert.Equal(
            first.Blocks.Select(x => (x.BlockId, x.Bounds)),
            second.Blocks.Select(x => (x.BlockId, x.Bounds)));
    }

    private static GroupSceneElement Group(
        string id,
        MmRect bounds) =>
        new(
            new SceneId(id),
            bounds,
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            []);

    private static DiagramSceneMetadata Metadata() =>
        new(
            "RIC18-V1",
            "1.0.0",
            "PROFILE",
            "G5-COLLISION",
            "INPUT",
            "PROJECTION");

    private static bool IsOnGrid(double value, double grid)
    {
        double quotient = value / grid;
        return Math.Abs(
            quotient - Math.Round(quotient)) < 1e-9;
    }

    private static void AssertNoOverlaps(
        IReadOnlyList<PositionedCompositionBlock> blocks)
    {
        for (int left = 0; left < blocks.Count; left++)
        {
            for (int right = left + 1; right < blocks.Count; right++)
            {
                Assert.False(
                    Overlaps(
                        blocks[left].Bounds,
                        blocks[right].Bounds),
                    $"Overlap: {blocks[left].BlockId} / {blocks[right].BlockId}");
            }
        }
    }

    private static bool Overlaps(MmRect a, MmRect b) =>
        a.X < b.Right &&
        a.Right > b.X &&
        a.Y < b.Bottom &&
        a.Bottom > b.Y;

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
