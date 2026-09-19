using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Rendering.Avalonia.HitTesting;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Rendering;

public sealed class SingleLineViewTests
{
    [AvaloniaFact]
    public void DrawingResources_FollowInheritedThemeVariantWithoutMutatingScene()
    {
        RIC18DrawingProfile profile = Profile();
        DiagramScene scene = Scene(
            "theme/element",
            new MmRect(10, 10, 12, 12),
            new MmRect(0, 0, 100, 60));
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene,
            DrawingProfile = profile
        };
        var window = new Window
        {
            Width = 400,
            Height = 300,
            RequestedThemeVariant = ThemeVariant.Dark,
            Content = view
        };

        window.Show();

        Assert.Equal(
            ThemeVariant.Dark,
            view.ActualThemeVariant);

        AvaloniaRenderResources dark =
            Resources(view);

        Assert.Same(
            Brushes.White,
            dark.ResolveTextBrush("TECH"));

        window.RequestedThemeVariant =
            ThemeVariant.Light;

        Assert.Equal(
            ThemeVariant.Light,
            view.ActualThemeVariant);

        AvaloniaRenderResources light =
            Resources(view);

        Assert.NotSame(
            dark,
            light);
        Assert.Same(
            Brushes.Black,
            light.ResolveTextBrush("TECH"));
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
    }

    [AvaloniaFact]
    public void SceneReplacement_IsAtomicAndDropsOldHitIndex()
    {
        DiagramScene sceneA = Scene(
            "scene-a/element",
            new MmRect(10, 10, 12, 12),
            new MmRect(0, 0, 100, 60));
        DiagramScene sceneB = Scene(
            "scene-b/element",
            new MmRect(60, 10, 12, 12),
            new MmRect(0, 0, 100, 60));
        string fingerprintA = DiagramSceneFingerprint.Compute(sceneA);
        string fingerprintB = DiagramSceneFingerprint.Compute(sceneB);

        var view = new SingleLineView
        {
            Scene = sceneA
        };
        Window window = Open(view);
        SceneId? primary = null;
        view.PrimaryHitChanged += (_, hit) =>
            primary = hit?.SceneElementId;

        Point pointA = ViewportTransform.SceneMmToDip(
            new MmPoint(16, 16),
            view.Viewport);
        window.MouseMove(
            pointA,
            RawInputModifiers.None);

        Assert.Equal(
            new SceneId("scene-a/element"),
            primary);

        view.Scene = sceneB;

        Point pointB = ViewportTransform.SceneMmToDip(
            new MmPoint(66, 16),
            view.Viewport);
        window.MouseMove(
            pointB,
            RawInputModifiers.None);

        Assert.Equal(
            new SceneId("scene-b/element"),
            primary);

        window.MouseMove(
            pointA,
            RawInputModifiers.None);

        Assert.Null(primary);
        Assert.Equal(
            fingerprintA,
            DiagramSceneFingerprint.Compute(sceneA));
        Assert.Equal(
            fingerprintB,
            DiagramSceneFingerprint.Compute(sceneB));
    }

    [AvaloniaFact]
    public void MouseWheelZoom_PreservesScenePointUnderCursor()
    {
        DiagramScene scene = Scene(
            "zoom/element",
            new MmRect(20, 20, 20, 20),
            new MmRect(0, 0, 120, 80));
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene
        };
        Window window = Open(view);
        var cursor = new Point(220, 140);
        MmPoint before =
            ViewportTransform.DipToSceneMm(
                cursor,
                view.Viewport);
        double zoomBefore = view.Viewport.Zoom;

        window.MouseWheel(
            cursor,
            new Vector(0, 1),
            RawInputModifiers.None);

        MmPoint after =
            ViewportTransform.DipToSceneMm(
                cursor,
                view.Viewport);

        Assert.True(view.Viewport.Zoom > zoomBefore);
        Assert.Equal(before.X, after.X, 9);
        Assert.Equal(before.Y, after.Y, 9);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
    }

    [AvaloniaFact]
    public void PanGestures_ChangeViewportOnly()
    {
        DiagramScene scene = Scene(
            "pan/element",
            new MmRect(20, 20, 20, 20),
            new MmRect(0, 0, 120, 80));
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene
        };
        Window window = Open(view);
        Vector initialPan = view.Viewport.PanDip;

        window.MouseDown(
            new Point(100, 100),
            MouseButton.Middle,
            RawInputModifiers.None);
        window.MouseMove(
            new Point(135, 115),
            RawInputModifiers.MiddleMouseButton);
        window.MouseUp(
            new Point(135, 115),
            MouseButton.Middle,
            RawInputModifiers.None);

        Assert.Equal(
            initialPan + new Vector(35, 15),
            view.Viewport.PanDip);

        view.Focus();
        window.KeyPress(
            Key.Space,
            RawInputModifiers.None,
            PhysicalKey.Space,
            " ");
        window.MouseDown(
            new Point(160, 120),
            MouseButton.Left,
            RawInputModifiers.None);
        window.MouseMove(
            new Point(180, 150),
            RawInputModifiers.LeftMouseButton);
        window.MouseUp(
            new Point(180, 150),
            MouseButton.Left,
            RawInputModifiers.None);
        window.KeyRelease(
            Key.Space,
            RawInputModifiers.None,
            PhysicalKey.Space,
            " ");

        Assert.Equal(
            initialPan + new Vector(55, 45),
            view.Viewport.PanDip);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
    }

    [AvaloniaFact]
    public void KeyboardViewportCommands_AreViewOnlyAndDeterministic()
    {
        DiagramScene scene = Scene(
            "keyboard/element",
            new MmRect(20, 20, 20, 20),
            new MmRect(0, 0, 120, 80));
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene
        };
        Window window = Open(view);
        view.Focus();
        double initialZoom = view.Viewport.Zoom;

        Press(
            window,
            Key.Add,
            PhysicalKey.NumPadAdd);
        Assert.True(view.Viewport.Zoom > initialZoom);

        Press(
            window,
            Key.Subtract,
            PhysicalKey.NumPadSubtract);
        Assert.Equal(initialZoom, view.Viewport.Zoom, 12);

        Press(
            window,
            Key.F,
            PhysicalKey.F);
        ViewportState fitted = view.Viewport;
        Rect mapped =
            ViewportTransform.SceneRectMmToDip(
                scene.Bounds,
                fitted);
        Assert.True(mapped.Left >= 0);
        Assert.True(mapped.Top >= 0);
        Assert.True(mapped.Right <= fitted.ViewportDip.Width);
        Assert.True(mapped.Bottom <= fitted.ViewportDip.Height);

        window.KeyPress(
            Key.F,
            RawInputModifiers.Shift,
            PhysicalKey.F,
            "F");
        window.KeyRelease(
            Key.F,
            RawInputModifiers.Shift,
            PhysicalKey.F,
            "F");

        Assert.Equal(fitted, view.Viewport);

        Press(
            window,
            Key.Home,
            PhysicalKey.Home);

        Assert.Equal(fitted, view.Viewport);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
    }

    [AvaloniaFact]
    public void PointerHover_UpdatesOverlayAndPrimaryHitOnly()
    {
        DiagramScene scene = Scene(
            "hover/element",
            new MmRect(10, 10, 12, 12),
            new MmRect(0, 0, 100, 60));
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);
        var view = new SingleLineView
        {
            Scene = scene
        };
        Window window = Open(view);
        HitTestResult? primary = null;
        view.PrimaryHitChanged += (_, hit) =>
            primary = hit;

        Point target = ViewportTransform.SceneMmToDip(
            new MmPoint(16, 16),
            view.Viewport);
        window.MouseMove(
            target,
            RawInputModifiers.None);

        Assert.NotNull(primary);
        Assert.Equal(
            new SceneId("hover/element"),
            primary!.SceneElementId);
        Assert.Equal(
            primary.SceneElementId,
            view.Overlay.Hovered);
        Assert.Empty(view.Overlay.Selected);

        window.MouseMove(
            new Point(390, 280),
            RawInputModifiers.None);

        Assert.Null(primary);
        Assert.Null(view.Overlay.Hovered);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
    }

    private static AvaloniaRenderResources Resources(
        SingleLineView view)
    {
        FieldInfo field =
            typeof(SingleLineView).GetField(
                "_resources",
                BindingFlags.Instance |
                BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "SingleLineView resources field was not found.");

        return Assert.IsType<AvaloniaRenderResources>(
            field.GetValue(view));
    }

    private static RIC18DrawingProfile Profile()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string solution =
                Path.Combine(
                    directory.FullName,
                    "UI_Unilineal.sln");

            if (File.Exists(solution))
            {
                return new Ric18DrawingProfileLoader()
                    .LoadDirectory(
                        Path.Combine(
                            directory.FullName,
                            "data",
                            "ric18",
                            "v1"));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate UI_Unilineal repository root.");
    }

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

        Assert.True(view.Viewport.ViewportDip.Width > 0);
        Assert.True(view.Viewport.ViewportDip.Height > 0);

        return window;
    }

    private static void Press(
        Window window,
        Key key,
        PhysicalKey physicalKey)
    {
        window.KeyPress(
            key,
            RawInputModifiers.None,
            physicalKey,
            null);
        window.KeyRelease(
            key,
            RawInputModifiers.None,
            physicalKey,
            null);
    }

    private static DiagramScene Scene(
        string id,
        MmRect elementBounds,
        MmRect sceneBounds) =>
        RenderingSceneFixtures.Scene(
            [
                RenderingSceneFixtures.Rectangle(
                    id,
                    elementBounds)
            ],
            sceneBounds);
}
