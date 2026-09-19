using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Rendering.Avalonia.Interaction;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Interaction;

public sealed class SingleLineViewInteractionTests
{
    [AvaloniaFact]
    public void View_RaisesOnlyTheIntentAllowedByExplicitMode()
    {
        DiagramScene scene = Scene();
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene
        };
        Window window = Open(view);

        LayoutMoveIntent? layout = null;
        ElectricalConnectionIntent? electrical = null;
        SelectionIntent? selection = null;

        view.LayoutMoveRequested +=
            (_, e) => layout = e.Intent;
        view.ElectricalProposalRequested +=
            (_, e) => electrical = e.Intent;
        view.SelectionChanged +=
            (_, e) => selection = e.Intent;

        view.InteractionMode =
            InteractionMode.Navigate;
        Click(
            window,
            view,
            new MmPoint(20, 15));

        Assert.NotNull(selection);
        Assert.Null(layout);
        Assert.Null(electrical);

        selection = null;
        view.InteractionMode =
            InteractionMode.Layout;
        Drag(
            window,
            view,
            new MmPoint(20, 15),
            new MmPoint(60, 15));

        Assert.Null(selection);
        Assert.NotNull(layout);
        Assert.Null(electrical);

        layout = null;
        view.InteractionMode =
            InteractionMode.Electrical;
        Drag(
            window,
            view,
            new MmPoint(20, 15),
            new MmPoint(60, 15));

        Assert.Null(selection);
        Assert.Null(layout);
        Assert.NotNull(electrical);
        Assert.Equal(
            InteractionStateKind.CommandPreview,
            view.InteractionState.Kind);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));

        window.Close();
    }

    [AvaloniaFact]
    public void Escape_CancelsLayoutGhostAndPreventsMoveIntent()
    {
        DiagramScene scene = Scene();
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene,
            InteractionMode = InteractionMode.Layout
        };
        Window window = Open(view);
        int moveRequests = 0;

        view.LayoutMoveRequested +=
            (_, _) => moveRequests++;

        Point source =
            ToDip(
                view,
                new MmPoint(20, 15));
        Point moved =
            ToDip(
                view,
                new MmPoint(40, 30));

        window.MouseDown(
            source,
            MouseButton.Left,
            RawInputModifiers.None);
        window.MouseMove(
            moved,
            RawInputModifiers.LeftMouseButton);

        Assert.NotNull(
            view.Overlay.LayoutGhostBounds);
        Assert.Equal(
            InteractionStateKind.DraggingLayout,
            view.InteractionState.Kind);

        window.KeyPress(
            Key.Escape,
            RawInputModifiers.None,
            PhysicalKey.Escape,
            null);
        window.KeyRelease(
            Key.Escape,
            RawInputModifiers.None,
            PhysicalKey.Escape,
            null);

        Assert.Equal(
            InteractionStateKind.Idle,
            view.InteractionState.Kind);
        Assert.Null(
            view.Overlay.LayoutGhostBounds);

        window.MouseUp(
            moved,
            MouseButton.Left,
            RawInputModifiers.None);

        Assert.Equal(
            0,
            moveRequests);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));

        window.Close();
    }

    private static void Click(
        Window window,
        SingleLineView view,
        MmPoint point)
    {
        Point dip =
            ToDip(
                view,
                point);

        window.MouseDown(
            dip,
            MouseButton.Left,
            RawInputModifiers.None);
        window.MouseUp(
            dip,
            MouseButton.Left,
            RawInputModifiers.None);
    }

    private static void Drag(
        Window window,
        SingleLineView view,
        MmPoint start,
        MmPoint end)
    {
        Point startDip =
            ToDip(
                view,
                start);
        Point endDip =
            ToDip(
                view,
                end);

        window.MouseDown(
            startDip,
            MouseButton.Left,
            RawInputModifiers.None);
        window.MouseMove(
            endDip,
            RawInputModifiers.LeftMouseButton);
        window.MouseUp(
            endDip,
            MouseButton.Left,
            RawInputModifiers.None);
    }

    private static Point ToDip(
        SingleLineView view,
        MmPoint point) =>
        ViewportTransform.SceneMmToDip(
            point,
            view.Viewport);

    private static Window Open(
        SingleLineView view)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = view
        };

        window.Show();
        view.Focus();

        Assert.True(
            view.Viewport.ViewportDip.Width > 0);
        Assert.True(
            view.Viewport.ViewportDip.Height > 0);

        return window;
    }

    private static DiagramScene Scene() =>
        new(
            new MmRect(0, 0, 100, 60),
            [
                Element(
                    "fixture/source",
                    SourceEntity(),
                    new MmRect(10, 10, 10, 10),
                    new SceneAnchor(
                        "out",
                        AnchorRole.PowerOut,
                        new MmPoint(20, 15),
                        AnchorDirection.Right)),
                Element(
                    "fixture/target",
                    TargetEntity(),
                    new MmRect(60, 10, 10, 10),
                    new SceneAnchor(
                        "in",
                        AnchorRole.PowerIn,
                        new MmPoint(60, 15),
                        AnchorDirection.Left))
            ],
            new DiagramSceneMetadata(
                "RIC18-V1",
                "1.0.0",
                "PROFILE",
                "G7",
                "INPUT",
                "PROJECTION"));

    private static GroupSceneElement Element(
        string id,
        EntityReference entity,
        MmRect bounds,
        SceneAnchor anchor) =>
        new(
            new SceneId(id),
            bounds,
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            entity,
            null,
            [],
            [anchor]);

    private static EntityReference SourceEntity() =>
        new(
            new EntityUid("source-1"),
            EntityKind.Source);

    private static EntityReference TargetEntity() =>
        new(
            new EntityUid("board-1"),
            EntityKind.Board);
}
