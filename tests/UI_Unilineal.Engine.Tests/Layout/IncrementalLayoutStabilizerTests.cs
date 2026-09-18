using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class IncrementalLayoutStabilizerTests
{
    [Fact]
    public void Stabilize_AddingBlockKeepsExistingLockedAndPinnedPositionsExact()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, "B1", "B2", "B3");
        PositionedLayout previous = Layout(
            ("summary/board/B1", new MmRect(7, 11, 20, 20)),
            ("summary/board/B2", new MmRect(43, 13, 20, 20)));
        PositionedLayout proposed = Layout(
            ("summary/board/B1", new MmRect(0, 0, 20, 20)),
            ("summary/board/B2", new MmRect(30, 0, 20, 20)),
            ("summary/board/B3", new MmRect(70, 0, 20, 20)));
        var state = State(
            new LayoutOverride(
                new EntityUid("B1"),
                new MmPoint(7, 11),
                LayoutLockMode.Locked),
            new LayoutOverride(
                new EntityUid("B2"),
                new MmPoint(43, 13),
                LayoutLockMode.Pinned));

        PositionedLayout stabilized =
            new IncrementalLayoutStabilizer().Stabilize(
                previous,
                proposed,
                composition,
                state,
                profile.Layout);

        Assert.Equal(
            previous.GetBlock("summary/board/B1").Bounds,
            stabilized.GetBlock("summary/board/B1").Bounds);
        Assert.Equal(
            previous.GetBlock("summary/board/B2").Bounds,
            stabilized.GetBlock("summary/board/B2").Bounds);
    }

    [Fact]
    public void Stabilize_AddingClearBlockKeepsUnaffectedAutoPositions()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, "B1", "B2", "B3");
        PositionedLayout previous = Layout(
            ("summary/board/B1", new MmRect(0, 0, 20, 20)),
            ("summary/board/B2", new MmRect(40, 0, 20, 20)));
        PositionedLayout proposed = Layout(
            ("summary/board/B1", new MmRect(0, 40, 20, 20)),
            ("summary/board/B2", new MmRect(40, 40, 20, 20)),
            ("summary/board/B3", new MmRect(80, 0, 20, 20)));

        PositionedLayout stabilized =
            new IncrementalLayoutStabilizer().Stabilize(
                previous,
                proposed,
                composition,
                State(),
                profile.Layout);

        Assert.Equal(
            previous.GetBlock("summary/board/B1").Bounds,
            stabilized.GetBlock("summary/board/B1").Bounds);
        Assert.Equal(
            previous.GetBlock("summary/board/B2").Bounds,
            stabilized.GetBlock("summary/board/B2").Bounds);
        Assert.Equal(
            proposed.GetBlock("summary/board/B3").Bounds,
            stabilized.GetBlock("summary/board/B3").Bounds);
    }

    [Fact]
    public void Stabilize_NewAutoBlockCollisionMovesNewBlockBeforeExistingBlocks()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, "B1", "B2", "B3");
        PositionedLayout previous = Layout(
            ("summary/board/B1", new MmRect(0, 0, 20, 20)),
            ("summary/board/B2", new MmRect(40, 0, 20, 20)));
        PositionedLayout proposed = Layout(
            ("summary/board/B1", new MmRect(0, 0, 20, 20)),
            ("summary/board/B2", new MmRect(40, 0, 20, 20)),
            ("summary/board/B3", new MmRect(0, 0, 20, 20)));

        PositionedLayout stabilized =
            new IncrementalLayoutStabilizer().Stabilize(
                previous,
                proposed,
                composition,
                State(),
                profile.Layout);

        Assert.Equal(
            previous.GetBlock("summary/board/B1").Bounds,
            stabilized.GetBlock("summary/board/B1").Bounds);
        Assert.Equal(
            previous.GetBlock("summary/board/B2").Bounds,
            stabilized.GetBlock("summary/board/B2").Bounds);
        Assert.NotEqual(
            proposed.GetBlock("summary/board/B3").Bounds,
            stabilized.GetBlock("summary/board/B3").Bounds);
        AssertNoOverlaps(stabilized.Blocks);
    }

    [Fact]
    public void Stabilize_RemovedBlockIsNotCarriedIntoNewLayout()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile, "B1");
        PositionedLayout previous = Layout(
            ("summary/board/B1", new MmRect(0, 0, 20, 20)),
            ("summary/board/B2", new MmRect(40, 0, 20, 20)));
        PositionedLayout proposed = Layout(
            ("summary/board/B1", new MmRect(10, 10, 20, 20)));

        PositionedLayout stabilized =
            new IncrementalLayoutStabilizer().Stabilize(
                previous,
                proposed,
                composition,
                State(),
                profile.Layout);

        Assert.Single(stabilized.Blocks);
        Assert.Equal(
            new MmRect(0, 0, 20, 20),
            stabilized.GetBlock("summary/board/B1").Bounds);
    }

    private static DrawingComposition Composition(
        RIC18DrawingProfile profile,
        params string[] entities) =>
        new(
            DrawingCompositionKind.Summary,
            new EntityReference(new EntityUid("P1"), EntityKind.Project),
            entities.Select(entity =>
                new CompositionBlock(
                    $"summary/board/{entity}",
                    "BOARD_SUMMARY_BLOCK",
                    "BoardSummary",
                    new EntityReference(
                        new EntityUid(entity),
                        EntityKind.Board),
                    new Dictionary<string, string>
                    {
                        ["NAME"] = entity
                    },
                    ProjectionStatus.Ok,
                    null)),
            [],
            "INPUT",
            DrawingProfileFingerprint.Compute(profile));

    private static PositionedLayout Layout(
        params (string Id, MmRect Bounds)[] blocks)
    {
        PositionedCompositionBlock[] positioned = blocks
            .Select(block =>
                new PositionedCompositionBlock(
                    block.Id,
                    block.Bounds))
            .ToArray();

        double right = positioned.Max(x => x.Bounds.Right) + 10;
        double bottom = positioned.Max(x => x.Bounds.Bottom) + 10;

        return new PositionedLayout(
            positioned,
            new MmRect(0, 0, right, bottom));
    }

    private static DiagramLayoutState State(
        params LayoutOverride[] overrides) =>
        new(
            DiagramSceneKind.ProjectSummary,
            new EntityUid("P1"),
            "1",
            overrides,
            null);

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
                        blocks[right].Bounds));
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
