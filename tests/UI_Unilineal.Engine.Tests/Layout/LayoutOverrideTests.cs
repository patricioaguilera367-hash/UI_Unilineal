using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class LayoutOverrideTests
{
    [Fact]
    public void Apply_EmptyState_PreservesAutomaticLayoutExactly()
    {
        RIC18DrawingProfile profile = Profile();
        PositionedLayout automatic = AutomaticLayout();
        DrawingComposition composition =
            Composition(profile);
        var state =
            new DiagramLayoutState(
                DiagramSceneKind.ProjectSummary,
                new EntityUid("P1"),
                "1",
                [],
                null);

        PositionedLayout result =
            new LayoutOverrideApplicator().Apply(
                automatic,
                composition,
                state,
                profile.Layout);

        Assert.Same(
            automatic,
            result);
    }

    [Fact]
    public void Apply_LockedEntityNeverMovesEvenWhenItCreatesOverlap()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile);
        PositionedLayout automatic = AutomaticLayout();
        var state = State(
            new LayoutOverride(
                new EntityUid("B1"),
                new MmPoint(20, 0),
                LayoutLockMode.Locked));

        PositionedLayout applied =
            new LayoutOverrideApplicator().Apply(
                automatic,
                composition,
                state,
                profile.Layout);

        Assert.Equal(
            new MmRect(20, 0, 20, 20),
            applied.GetBlock("summary/board/B1").Bounds);
    }

    [Fact]
    public void Apply_PinnedEntityKeepsPreferredPositionWhenClear()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile);
        PositionedLayout automatic = AutomaticLayout();
        var state = State(
            new LayoutOverride(
                new EntityUid("B2"),
                new MmPoint(60, 40),
                LayoutLockMode.Pinned));

        PositionedLayout applied =
            new LayoutOverrideApplicator().Apply(
                automatic,
                composition,
                state,
                profile.Layout);

        Assert.Equal(
            new MmRect(60, 40, 20, 20),
            applied.GetBlock("summary/board/B2").Bounds);
    }

    [Fact]
    public void Apply_PinnedEntityMovesWhenPreferredPositionConflictsWithLocked()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile);
        PositionedLayout automatic = AutomaticLayout();
        var state = new DiagramLayoutState(
            DiagramSceneKind.ProjectSummary,
            new EntityUid("P1"),
            "1",
            [
                new LayoutOverride(
                    new EntityUid("B1"),
                    new MmPoint(40, 40),
                    LayoutLockMode.Locked),
                new LayoutOverride(
                    new EntityUid("B2"),
                    new MmPoint(40, 40),
                    LayoutLockMode.Pinned)
            ],
            null);

        PositionedLayout applied =
            new LayoutOverrideApplicator().Apply(
                automatic,
                composition,
                state,
                profile.Layout);

        Assert.Equal(
            new MmRect(40, 40, 20, 20),
            applied.GetBlock("summary/board/B1").Bounds);
        Assert.NotEqual(
            new MmRect(40, 40, 20, 20),
            applied.GetBlock("summary/board/B2").Bounds);
        Assert.False(
            Overlaps(
                applied.GetBlock("summary/board/B1").Bounds,
                applied.GetBlock("summary/board/B2").Bounds));
    }

    [Fact]
    public void Apply_AutoOverrideDoesNotForcePreferredPosition()
    {
        RIC18DrawingProfile profile = Profile();
        DrawingComposition composition = Composition(profile);
        PositionedLayout automatic = AutomaticLayout();
        var state = State(
            new LayoutOverride(
                new EntityUid("B2"),
                new MmPoint(99, 99),
                LayoutLockMode.Auto));

        PositionedLayout applied =
            new LayoutOverrideApplicator().Apply(
                automatic,
                composition,
                state,
                profile.Layout);

        Assert.Equal(
            automatic.GetBlock("summary/board/B2").Bounds,
            applied.GetBlock("summary/board/B2").Bounds);
    }

    private static DrawingComposition Composition(
        RIC18DrawingProfile profile) =>
        new(
            DrawingCompositionKind.Summary,
            new EntityReference(new EntityUid("P1"), EntityKind.Project),
            [
                Block("summary/board/B1", "B1"),
                Block("summary/board/B2", "B2")
            ],
            [],
            "INPUT",
            DrawingProfileFingerprint.Compute(profile));

    private static CompositionBlock Block(string id, string uid) =>
        new(
            id,
            "BOARD_SUMMARY_BLOCK",
            "BoardSummary",
            new EntityReference(new EntityUid(uid), EntityKind.Board),
            new Dictionary<string, string>
            {
                ["NAME"] = uid
            },
            ProjectionStatus.Ok,
            null);

    private static PositionedLayout AutomaticLayout() =>
        new(
            [
                new PositionedCompositionBlock(
                    "summary/board/B1",
                    new MmRect(0, 0, 20, 20)),
                new PositionedCompositionBlock(
                    "summary/board/B2",
                    new MmRect(40, 0, 20, 20))
            ],
            new MmRect(0, 0, 100, 100));

    private static DiagramLayoutState State(LayoutOverride value) =>
        new(
            DiagramSceneKind.ProjectSummary,
            new EntityUid("P1"),
            "1",
            [value],
            null);

    private static bool Overlaps(MmRect a, MmRect b) =>
        a.X < b.Right &&
        a.Right > b.X &&
        a.Y < b.Bottom &&
        a.Bottom > b.Y;

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
