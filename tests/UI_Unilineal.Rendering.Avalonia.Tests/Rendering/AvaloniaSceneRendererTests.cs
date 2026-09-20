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
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Rendering.Avalonia.Rendering;
using UI_Unilineal.Rendering.Avalonia.Tests.Fixtures;
using UI_Unilineal.Rendering.Avalonia.Viewport;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Rendering;

public sealed class AvaloniaSceneRendererTests
{
    [Fact]
    public void BuildRenderList_CullsAndOrdersByLayerZIndexAndSceneId()
    {
        var visibleSecond = RenderingSceneFixtures.Rectangle(
            "visible/b",
            new MmRect(10, 10, 5, 5),
            SceneLayer.Symbol,
            20);
        var visibleFirst = RenderingSceneFixtures.Rectangle(
            "visible/a",
            new MmRect(8, 8, 5, 5),
            SceneLayer.Symbol,
            20);
        var visiblePower = RenderingSceneFixtures.Rectangle(
            "visible/power",
            new MmRect(6, 6, 5, 5),
            SceneLayer.Power,
            50);
        var offscreen = RenderingSceneFixtures.Rectangle(
            "offscreen/element",
            new MmRect(250, 250, 10, 10),
            SceneLayer.Background,
            0);
        DiagramScene scene = RenderingSceneFixtures.Scene(
            [visibleSecond, offscreen, visibleFirst, visiblePower],
            new MmRect(0, 0, 300, 300));
        var index = new SceneSpatialIndex(scene);
        var viewport = new ViewportState(
            zoom: 1,
            panDip: default,
            viewportDip: new Size(400, 300));
        var renderer = new AvaloniaSceneRenderer();

        IReadOnlyList<SceneElement> result =
            renderer.BuildRenderList(
                viewport,
                index);

        Assert.Equal(
            ["visible/power", "visible/a", "visible/b"],
            result.Select(element => element.Id.Value));
    }

    [Fact]
    public void ShouldFillCircle_MainBusJunctionNode_ReturnsTrue()
    {
        var circle = new CircleSceneElement(
            new SceneId("board/bus/node/TAP-1"),
            new MmRect(10, 10, 4, 4),
            SceneLayer.Power,
            20,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["compositionRole"] = "MainBus"
            },
            new MmPoint(12, 12),
            2,
            "BUS");

        Assert.True(AvaloniaSceneRenderer.ShouldFillCircle(circle));
    }

    [AvaloniaFact]
    public void Render_AllSceneElementKinds_DoesNotMutateFingerprint()
    {
        RIC18DrawingProfile profile = Profile();
        DiagramScene scene = SceneWithEveryElementKind();
        var index = new SceneSpatialIndex(scene);
        var viewport = FittedViewport(scene);
        var resources = new AvaloniaRenderResources(
            profile,
            InteractiveThemeKind.Light);
        var renderer = new AvaloniaSceneRenderer();
        string before = DiagramSceneFingerprint.Compute(scene);

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(1000, 700),
            new Vector(96, 96));
        using DrawingContext context =
            bitmap.CreateDrawingContext();

        renderer.Render(
            context,
            scene,
            profile,
            viewport,
            index,
            resources);

        string after = DiagramSceneFingerprint.Compute(scene);

        Assert.Equal(before, after);
        Assert.Equal(8, scene.Elements.Count);
    }

    [AvaloniaFact]
    public void Render_LabelOnlySymbol_DoesNotThrow()
    {
        RIC18DrawingProfile profile = Profile();
        var symbol = new SymbolSceneElement(
            new SceneId("fixture/label-only"),
            new MmRect(10, 10, 28, 6),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            "SERVICE_TARIFF_TEXT");
        var text = new TextSceneElement(
            new SceneId("fixture/label-only/text"),
            new MmRect(10, 10, 28, 6),
            SceneLayer.Text,
            30,
            SceneVisibility.Both,
            null,
            null,
            "Tarifa: BT1",
            "LABEL_SMALL");
        DiagramScene scene =
            RenderingSceneFixtures.Scene(
                [symbol, text],
                new MmRect(0, 0, 50, 30));

        RenderGenerated(
            scene,
            profile);
    }

    [AvaloniaFact]
    public void Playground_CurrentBoardDetailAndSymbolGallery_RenderWithoutException()
    {
        PlaygroundFixture fixture =
            PlaygroundFixtureFactory.Create();
        var engine =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics());

        SingleLineLayoutResult detail =
            engine.LayoutBoardDetail(
                fixture.Projection,
                new EntityUid("B1"),
                fixture.Profile);

        Assert.True(detail.Success);
        RenderGenerated(
            Assert.IsType<DiagramScene>(detail.Scene),
            fixture.Profile);

        DiagramScene gallery =
            SymbolGallerySceneBuilder.Build(
                fixture.Profile);

        RenderGenerated(
            gallery,
            fixture.Profile);
    }

    [Fact]
    public void Playground_RcdNeutralAnchorsStayOnNeutralSideOfPowerAxis()
    {
        PlaygroundFixture fixture =
            PlaygroundFixtureFactory.Create();
        SingleLineLayoutResult detail =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics())
                .LayoutBoardDetail(
                    fixture.Projection,
                    new EntityUid("B2"),
                    fixture.Profile);

        Assert.True(
            detail.Success,
            detail.Failure?.Message);

        DiagramScene scene =
            Assert.IsType<DiagramScene>(
                detail.Scene);
        GroupSceneElement[] rcds =
            scene.Elements
                .OfType<GroupSceneElement>()
                .Where(element =>
                    element.Metadata.TryGetValue(
                        "compositionRole",
                        out string? role) &&
                    role == "DifferentialProtection")
                .ToArray();

        Assert.NotEmpty(rcds);

        foreach (GroupSceneElement rcd in rcds)
        {
            SceneAnchor power =
                rcd.Anchors.Single(anchor =>
                    anchor.Id == "IN");
            SceneAnchor neutralIn =
                rcd.Anchors.Single(anchor =>
                    anchor.Id == "N_IN");
            SceneAnchor neutralOut =
                rcd.Anchors.Single(anchor =>
                    anchor.Id == "N_OUT");

            Assert.True(
                neutralIn.Point.X > power.Point.X);
            Assert.True(
                neutralOut.Point.X > power.Point.X);
        }
    }

    [AvaloniaFact]
    public void InteractionOverlay_RenderingDoesNotEnterOrMutateScene()
    {
        RIC18DrawingProfile profile = Profile();
        RectangleSceneElement target =
            RenderingSceneFixtures.Rectangle(
                "overlay/target",
                new MmRect(10, 10, 20, 15));
        DiagramScene scene =
            RenderingSceneFixtures.Scene(
                [target],
                new MmRect(0, 0, 60, 40));
        var viewport = FittedViewport(scene);
        var resources = new AvaloniaRenderResources(
            profile,
            InteractiveThemeKind.Dark);
        var overlay = new InteractionOverlayState(
            target.Id,
            new HashSet<SceneId> { target.Id });
        int elementCount = scene.Elements.Count;
        string fingerprint =
            DiagramSceneFingerprint.Compute(scene);

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(1000, 700),
            new Vector(96, 96));
        using DrawingContext context =
            bitmap.CreateDrawingContext();

        new InteractionOverlayRenderer().Render(
            context,
            scene,
            viewport,
            overlay,
            resources);

        Assert.Equal(elementCount, scene.Elements.Count);
        Assert.Equal(
            fingerprint,
            DiagramSceneFingerprint.Compute(scene));
        Assert.DoesNotContain(
            scene.Elements,
            element => element.Layer == SceneLayer.Interaction);
    }

    [AvaloniaFact]
    public void HeadlessRender_G5SummaryAndNestedDetail_CompleteWithoutException()
    {
        RIC18DrawingProfile profile = Profile();
        ProjectionBuildResult projectionResult =
            new SingleLineProjectionBuilder().Build(
                NestedInput());
        SingleLineProjection projection =
            Assert.IsType<SingleLineProjection>(
                projectionResult.Projection);
        var engine =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics());

        SingleLineLayoutResult summary =
            engine.LayoutSummary(
                projection,
                profile);
        SingleLineLayoutResult detail =
            engine.LayoutBoardDetail(
                projection,
                new EntityUid("B1"),
                profile);

        Assert.True(summary.Success);
        Assert.True(detail.Success);

        RenderGenerated(
            Assert.IsType<DiagramScene>(summary.Scene),
            profile);
        RenderGenerated(
            Assert.IsType<DiagramScene>(detail.Scene),
            profile);
    }

    private static DiagramScene SceneWithEveryElementKind()
    {
        var line = new LineSceneElement(
            new SceneId("fixture/line"),
            new MmRect(5, 5, 20, 0.001),
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null,
            null,
            new MmPoint(5, 5),
            new MmPoint(25, 5),
            "POWER");
        var polyline = new PolylineSceneElement(
            new SceneId("fixture/polyline"),
            new MmRect(5, 12, 25, 10),
            SceneLayer.Power,
            11,
            SceneVisibility.Both,
            null,
            null,
            [new MmPoint(5, 12), new MmPoint(15, 22), new MmPoint(30, 12)],
            "POWER");
        var rectangle = new RectangleSceneElement(
            new SceneId("fixture/rectangle"),
            new MmRect(35, 5, 15, 12),
            SceneLayer.Symbol,
            15,
            SceneVisibility.Both,
            null,
            null,
            "POWER");
        var circle = new CircleSceneElement(
            new SceneId("fixture/circle"),
            new MmRect(55, 5, 12, 12),
            SceneLayer.Symbol,
            16,
            SceneVisibility.Both,
            null,
            null,
            new MmPoint(61, 11),
            6,
            "POWER");
        var path = new PathSceneElement(
            new SceneId("fixture/path"),
            new MmRect(72, 5, 18, 12),
            SceneLayer.Symbol,
            17,
            SceneVisibility.Both,
            null,
            null,
            "M 72 5 L 90 17",
            "POWER");
        var text = new TextSceneElement(
            new SceneId("fixture/text"),
            new MmRect(5, 30, 35, 6),
            SceneLayer.Text,
            30,
            SceneVisibility.Both,
            null,
            null,
            "TD-01",
            "TECH");
        var symbol = new SymbolSceneElement(
            new SceneId("fixture/symbol"),
            new MmRect(50, 27, 12, 16),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            null,
            null,
            "BREAKER");
        var group = new GroupSceneElement(
            new SceneId("fixture/group"),
            new MmRect(0, 0, 100, 50),
            SceneLayer.Symbol,
            5,
            SceneVisibility.Both,
            null,
            null,
            [line.Id, polyline.Id, rectangle.Id, circle.Id, path.Id, text.Id, symbol.Id]);

        return RenderingSceneFixtures.Scene(
            [group, symbol, text, path, circle, rectangle, polyline, line],
            new MmRect(0, 0, 100, 50));
    }

    private static void RenderGenerated(
        DiagramScene scene,
        RIC18DrawingProfile profile)
    {
        var viewport = FittedViewport(scene);
        var index = new SceneSpatialIndex(scene);
        var resources = new AvaloniaRenderResources(
            profile,
            InteractiveThemeKind.Light);

        using var bitmap = new RenderTargetBitmap(
            new PixelSize(1000, 700),
            new Vector(96, 96));
        using DrawingContext context =
            bitmap.CreateDrawingContext();

        new AvaloniaSceneRenderer().Render(
            context,
            scene,
            profile,
            viewport,
            index,
            resources);
    }

    private static ViewportState FittedViewport(
        DiagramScene scene)
    {
        var initial = new ViewportState(
            zoom: 1,
            panDip: default,
            viewportDip: new Size(1000, 700));

        return new ViewportController().FitScene(
            initial,
            scene.Bounds,
            marginDip: 24);
    }

    private static RIC18DrawingProfile Profile()
    {
        string repositoryRoot = FindRepositoryRoot();

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

    private static SingleLineInput NestedInput()
    {
        var project = new ProjectInput(
            new EntityUid("P2"),
            "P2",
            "Proyecto anidado",
            OperationalState.Active);
        var source = new SourceInput(
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
        var main = new BoardInput(
            new EntityUid("B1"),
            1,
            "TGBT",
            "Tablero general",
            BoardRole.Main,
            "Sala eléctrica",
            400m,
            3,
            OperationalState.Active,
            DataState.Complete);
        var downstream = new BoardInput(
            new EntityUid("B2"),
            2,
            "TDA",
            "Tablero derivado",
            BoardRole.Distribution,
            "Nivel 1",
            400m,
            3,
            OperationalState.Active,
            DataState.Complete);
        var feeder = new CircuitInput(
            new EntityUid("C4"),
            main.Uid,
            4,
            "C04",
            "Alimentador TDA",
            CircuitRole.Feeder,
            "ALIMENTADOR",
            "3F",
            400m,
            1m,
            25m,
            null,
            null,
            new ConductorInput(
                "CU",
                "THHN",
                10m,
                10m,
                4,
                null),
            OperationalState.Active,
            DataState.Complete);
        var final = new CircuitInput(
            new EntityUid("C2"),
            downstream.Uid,
            1,
            "C01",
            "Alumbrado",
            CircuitRole.Final,
            "ALUMBRADO",
            "1F",
            230m,
            1m,
            12m,
            null,
            null,
            new ConductorInput(
                "CU",
                "THHN",
                2.5m,
                2.5m,
                2,
                null),
            OperationalState.Active,
            DataState.Complete);
        var sourceSupply = new SupplyConnection(
            new EntityUid("SC1"),
            new EntityReference(source.Uid, EntityKind.Source),
            null,
            main.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);
        var downstreamSupply = new SupplyConnection(
            new EntityUid("SC2"),
            new EntityReference(main.Uid, EntityKind.Board),
            feeder.Uid,
            downstream.Uid,
            SupplyRole.Normal,
            0,
            true,
            OperationalState.Active,
            DataState.Complete);
        var feederBreaker = new ProtectionInput(
            new EntityUid("PR4"),
            new EntityReference(feeder.Uid, EntityKind.Circuit),
            new EntityReference(feeder.Uid, EntityKind.Circuit),
            ProtectionKind.Breaker,
            ProtectionRole.Feeder,
            3,
            32m,
            10m,
            "C",
            null,
            null,
            null,
            null,
            OperationalState.Active,
            DataState.Complete);
        var finalBreaker = new ProtectionInput(
            new EntityUid("PR2"),
            new EntityReference(final.Uid, EntityKind.Circuit),
            new EntityReference(final.Uid, EntityKind.Circuit),
            ProtectionKind.Breaker,
            ProtectionRole.Branch,
            2,
            10m,
            6m,
            "C",
            null,
            null,
            null,
            null,
            OperationalState.Active,
            DataState.Complete);

        return new SingleLineInput(
            project,
            [source],
            [main, downstream],
            [],
            [feeder, final],
            [sourceSupply, downstreamSupply],
            [feederBreaker, finalBreaker],
            [],
            [],
            new SingleLineInputMetadata(
                "1",
                "TEST",
                "nested-rendering"));
    }
}
