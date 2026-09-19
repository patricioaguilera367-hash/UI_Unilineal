using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Rendering.Avalonia.HitTesting;
using UI_Unilineal.Rendering.Avalonia.Interaction;
using UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Interaction;

public sealed class InteractionInputTranslatorTests
{
    [Theory]
    [InlineData(InteractionMode.Navigate)]
    [InlineData(InteractionMode.Layout)]
    [InlineData(InteractionMode.Electrical)]
    public void SameDrag_ProducesOnlyTheIntentFamilyAllowedByMode(
        InteractionMode mode)
    {
        DiagramScene scene = Scene();
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var translator = new InteractionInputTranslator();
        translator.SetMode(mode);

        InteractionTranslationResult result =
            Drag(
                translator,
                scene,
                SourceAnchorHit(),
                TargetAnchorHit());

        Assert.Equal(
            mode == InteractionMode.Navigate,
            result.Selection is not null);
        Assert.Equal(
            mode == InteractionMode.Layout,
            result.LayoutMove is not null);
        Assert.Equal(
            mode == InteractionMode.Electrical,
            result.ElectricalConnection is not null);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
    }

    [Fact]
    public void LayoutDrag_CanNeverProduceElectricalIntent()
    {
        DiagramScene scene = Scene();
        var translator = new InteractionInputTranslator();
        translator.SetMode(
            InteractionMode.Layout);

        InteractionTranslationResult result =
            Drag(
                translator,
                scene,
                SourceAnchorHit(),
                TargetAnchorHit());

        Assert.NotNull(result.LayoutMove);
        Assert.Null(result.ElectricalConnection);
    }

    [Fact]
    public void ElectricalDrag_FromNonAnchorProducesNoProposalIntent()
    {
        DiagramScene scene = Scene();
        var translator = new InteractionInputTranslator();
        translator.SetMode(
            InteractionMode.Electrical);

        translator.PointerPressed(
            scene,
            new HitTestResult(
                new SceneId("fixture/source"),
                SourceEntity(),
                HitKind.Symbol,
                null,
                0,
                600),
            new MmPoint(12, 12));

        InteractionTranslationResult result =
            translator.PointerReleased(
                scene,
                TargetAnchorHit(),
                new MmPoint(62, 12));

        Assert.Null(result.ElectricalConnection);
    }

    [Theory]
    [InlineData(InteractionMode.Layout)]
    [InlineData(InteractionMode.Electrical)]
    [InlineData(InteractionMode.Navigate)]
    public void EscapeCancel_ReturnsTransientGestureToIdle(
        InteractionMode mode)
    {
        DiagramScene scene = Scene();
        var translator = new InteractionInputTranslator();
        translator.SetMode(mode);

        HitTestResult? hit =
            mode == InteractionMode.Navigate
                ? null
                : SourceAnchorHit();

        translator.PointerPressed(
            scene,
            hit,
            new MmPoint(12, 12));
        translator.PointerMoved(
            scene,
            hit,
            new MmPoint(24, 24));

        InteractionTranslationResult cancelled =
            translator.Cancel();

        Assert.True(cancelled.Cancelled);
        Assert.Equal(
            InteractionStateKind.Idle,
            translator.State.Kind);
        Assert.Null(cancelled.LayoutMove);
        Assert.Null(cancelled.ElectricalConnection);
    }

    [Fact]
    public void EscapeCancel_ClosesElectricalCommandPreview()
    {
        DiagramScene scene = Scene();
        var translator = new InteractionInputTranslator();
        translator.SetMode(
            InteractionMode.Electrical);

        InteractionTranslationResult completed =
            Drag(
                translator,
                scene,
                SourceAnchorHit(),
                TargetAnchorHit());

        Assert.NotNull(
            completed.ElectricalConnection);
        Assert.Equal(
            InteractionStateKind.CommandPreview,
            translator.State.Kind);

        InteractionTranslationResult cancelled =
            translator.Cancel();

        Assert.True(cancelled.Cancelled);
        Assert.Equal(
            InteractionStateKind.Idle,
            translator.State.Kind);
    }

    private static InteractionTranslationResult Drag(
        InteractionInputTranslator translator,
        DiagramScene scene,
        HitTestResult source,
        HitTestResult target)
    {
        translator.PointerPressed(
            scene,
            source,
            new MmPoint(12, 12));
        translator.PointerMoved(
            scene,
            source,
            new MmPoint(34, 24));

        return translator.PointerReleased(
            scene,
            target,
            new MmPoint(62, 12));
    }

    private static HitTestResult SourceAnchorHit() =>
        new(
            new SceneId("fixture/source"),
            SourceEntity(),
            HitKind.Anchor,
            "out",
            0,
            700);

    private static HitTestResult TargetAnchorHit() =>
        new(
            new SceneId("fixture/target"),
            TargetEntity(),
            HitKind.Anchor,
            "in",
            0,
            700);

    private static EntityReference SourceEntity() =>
        new(
            new EntityUid("source-1"),
            EntityKind.Source);

    private static EntityReference TargetEntity() =>
        new(
            new EntityUid("board-1"),
            EntityKind.Board);

    private static DiagramScene Scene() =>
        RenderingSceneFixtures.Scene(
            [
                new GroupSceneElement(
                    new SceneId("fixture/source"),
                    new MmRect(10, 10, 10, 10),
                    SceneLayer.Symbol,
                    20,
                    SceneVisibility.Both,
                    SourceEntity(),
                    null,
                    [],
                    [
                        new SceneAnchor(
                            "out",
                            AnchorRole.PowerOut,
                            new MmPoint(20, 15),
                            AnchorDirection.Right)
                    ]),
                new GroupSceneElement(
                    new SceneId("fixture/target"),
                    new MmRect(60, 10, 10, 10),
                    SceneLayer.Symbol,
                    20,
                    SceneVisibility.Both,
                    TargetEntity(),
                    null,
                    [],
                    [
                        new SceneAnchor(
                            "in",
                            AnchorRole.PowerIn,
                            new MmPoint(60, 15),
                            AnchorDirection.Left)
                    ])
            ],
            new MmRect(0, 0, 100, 60));
}
