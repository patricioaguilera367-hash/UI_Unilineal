using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Rendering.Avalonia.HitTesting;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Rendering;

public sealed class G6HardeningTests
{
    [AvaloniaFact]
    public void LargeG5Scene_CullsRendersAndHitTestsWithoutMutation()
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineProjection projection =
            Project(GeneratedInput(48));
        SingleLineLayoutResult layout =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics())
                .LayoutBoardDetail(
                    projection,
                    new EntityUid("B1"),
                    profile);

        Assert.True(
            layout.Success,
            layout.Failure?.Message);

        DiagramScene scene =
            Assert.IsType<DiagramScene>(
                layout.Scene);
        string before =
            DiagramSceneFingerprint.Compute(scene);

        var spatial =
            new SceneSpatialIndex(scene);
        GroupSceneElement target =
            scene.Elements
                .OfType<GroupSceneElement>()
                .First();
        MmPoint targetCenter =
            Center(target.Bounds);
        var viewport =
            new ViewportController().CenterOn(
                new ViewportState(
                    zoom: 4,
                    panDip: default,
                    viewportDip: new Size(800, 600)),
                targetCenter);
        MmRect visibleBounds =
            ViewportTransform.DipRectToSceneMm(
                new Rect(
                    0,
                    0,
                    viewport.ViewportDip.Width,
                    viewport.ViewportDip.Height),
                viewport);

        IReadOnlyList<SceneElement> candidates =
            spatial.Query(visibleBounds);

        Assert.NotEmpty(candidates);
        Assert.True(
            candidates.Count < scene.Elements.Count);
        Assert.All(
            candidates,
            element => Assert.True(
                Intersects(
                    element.Bounds,
                    visibleBounds),
                element.Id.Value));

        var renderer =
            new AvaloniaSceneRenderer();
        IReadOnlyList<SceneElement> renderList =
            renderer.BuildRenderList(
                viewport,
                spatial);

        Assert.NotEmpty(renderList);
        Assert.True(
            renderList.Count < scene.Elements.Count);

        var resources =
            new AvaloniaRenderResources(
                profile,
                InteractiveThemeKind.Light);
        using var bitmap =
            new RenderTargetBitmap(
                new PixelSize(800, 600),
                new Vector(96, 96));
        using DrawingContext context =
            bitmap.CreateDrawingContext();

        renderer.Render(
            context,
            scene,
            profile,
            viewport,
            spatial,
            resources);

        var hitIndex =
            new HitTestIndex(
                scene,
                spatial);
        Point pointer =
            ViewportTransform.SceneMmToDip(
                targetCenter,
                viewport);

        HitTestResult[] first =
            hitIndex.HitTest(
                    pointer,
                    viewport,
                    new HitTestPolicy())
                .ToArray();
        HitTestResult[] second =
            hitIndex.HitTest(
                    pointer,
                    viewport,
                    new HitTestPolicy())
                .ToArray();

        Assert.NotEmpty(first);
        Assert.Equal(
            first.Select(Key),
            second.Select(Key));
        Assert.Equal(
            before,
            DiagramSceneFingerprint.Compute(scene));
    }

    private static string Key(
        HitTestResult hit) =>
        $"{hit.Kind}|{hit.SceneElementId.Value}|{hit.AnchorId}";

    private static MmPoint Center(
        MmRect bounds) =>
        new(
            bounds.X + (bounds.Width / 2.0),
            bounds.Y + (bounds.Height / 2.0));

    private static bool Intersects(
        MmRect first,
        MmRect second) =>
        first.X <= second.Right &&
        first.Right >= second.X &&
        first.Y <= second.Bottom &&
        first.Bottom >= second.Y;

    private static SingleLineProjection Project(
        SingleLineInput input)
    {
        ProjectionBuildResult result =
            new SingleLineProjectionBuilder().Build(input);

        Assert.True(
            result.Success,
            string.Join(
                Environment.NewLine,
                result.Validation.Issues));

        return Assert.IsType<SingleLineProjection>(
            result.Projection);
    }

    private static SingleLineInput GeneratedInput(
        int circuitCount)
    {
        var project =
            new ProjectInput(
                new EntityUid("P-G6-STRESS"),
                "P-G6-STRESS",
                $"G6 stress {circuitCount}",
                OperationalState.Active);
        var source =
            new SourceInput(
                new EntityUid("S1"),
                "EMPALME",
                "Empalme",
                SourceKind.Utility,
                "3F",
                400m,
                3,
                true,
                OperationalState.Active,
                DataState.Complete);
        var board =
            new BoardInput(
                new EntityUid("B1"),
                1,
                "TGBT",
                "Tablero stress",
                BoardRole.Main,
                "Sala eléctrica",
                400m,
                3,
                OperationalState.Active,
                DataState.Complete);
        var supply =
            new SupplyConnection(
                new EntityUid("SC1"),
                new EntityReference(
                    source.Uid,
                    EntityKind.Source),
                null,
                board.Uid,
                SupplyRole.Normal,
                0,
                true,
                OperationalState.Active,
                DataState.Complete);

        var circuits =
            new List<CircuitInput>(
                circuitCount);
        var protections =
            new List<ProtectionInput>(
                circuitCount);

        for (int index = 1;
             index <= circuitCount;
             index++)
        {
            string suffix =
                index.ToString(
                    "D3",
                    System.Globalization.CultureInfo.InvariantCulture);
            var circuit =
                new CircuitInput(
                    new EntityUid($"C{suffix}"),
                    board.Uid,
                    index,
                    $"C{suffix}",
                    $"Carga {suffix}",
                    CircuitRole.Final,
                    "CARGA",
                    index % 3 == 0
                        ? "3F"
                        : "1F",
                    index % 3 == 0
                        ? 400m
                        : 230m,
                    1m,
                    10m + index,
                    null,
                    null,
                    new ConductorInput(
                        "CU",
                        "THHN",
                        2.5m,
                        2.5m,
                        index % 3 == 0
                            ? 4
                            : 2,
                        null),
                    OperationalState.Active,
                    DataState.Complete);

            circuits.Add(circuit);
            protections.Add(
                new ProtectionInput(
                    new EntityUid($"PR{suffix}"),
                    new EntityReference(
                        circuit.Uid,
                        EntityKind.Circuit),
                    new EntityReference(
                        circuit.Uid,
                        EntityKind.Circuit),
                    ProtectionKind.Breaker,
                    ProtectionRole.Branch,
                    index % 3 == 0
                        ? 3
                        : 2,
                    10m + index,
                    6m,
                    "C",
                    null,
                    null,
                    null,
                    null,
                    OperationalState.Active,
                    DataState.Complete));
        }

        return new SingleLineInput(
            project,
            [source],
            [board],
            [],
            circuits,
            [supply],
            protections,
            [],
            [],
            new SingleLineInputMetadata(
                "1",
                "TEST",
                $"g6-stress-{circuitCount}"));
    }

    private static RIC18DrawingProfile Profile()
    {
        string repositoryRoot =
            FindRepositoryRoot();

        return new Ric18DrawingProfileLoader()
            .LoadDirectory(
                Path.Combine(
                    repositoryRoot,
                    "data",
                    "ric18",
                    "v1"));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory =
            new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "UI_Unilineal.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate UI_Unilineal repository root.");
    }
}
