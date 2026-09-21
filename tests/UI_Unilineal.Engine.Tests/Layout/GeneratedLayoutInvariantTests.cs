using System.Diagnostics;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class GeneratedLayoutInvariantTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(24)]
    [InlineData(48)]
    [InlineData(100)]
    public void GeneratedBoardDetail_SatisfiesStructuralInvariants(
        int circuitCount)
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineProjection projection =
            Project(GeneratedInput(circuitCount, reverse: false));

        var stopwatch = Stopwatch.StartNew();
        SingleLineLayoutResult result =
            new SingleLineLayoutEngine(new DeterministicTextMetrics()).LayoutBoardDetail(
                projection,
                new EntityUid("B1"),
                profile);
        stopwatch.Stop();

        Assert.True(
            result.Success,
            $"count={circuitCount}; elapsed={stopwatch.Elapsed}; " +
            $"failure={result.Failure?.Stage}/{result.Failure?.Code}: " +
            result.Failure?.Message);

        DiagramScene scene =
            Assert.IsType<DiagramScene>(result.Scene);

        DiagramSceneValidationResult strict =
            new DiagramSceneValidator().Validate(
                scene,
                SceneValidationMode.Strict);

        Assert.False(
            strict.HasErrors,
            $"count={circuitCount}; elapsed={stopwatch.Elapsed}{Environment.NewLine}" +
            string.Join(Environment.NewLine, strict.Issues));

        Assert.All(
            scene.Elements,
            element =>
            {
                Assert.True(FinitePositive(element.Bounds));
                Assert.All(
                    element.Anchors,
                    anchor =>
                    {
                        Assert.True(double.IsFinite(anchor.Point.X));
                        Assert.True(double.IsFinite(anchor.Point.Y));
                        Assert.True(element.Bounds.Contains(anchor.Point));
                    });
            });

        PolylineSceneElement[] routes = scene.Elements
            .OfType<PolylineSceneElement>()
            .Where(element =>
                element.Metadata.ContainsKey("connectionId"))
            .ToArray();

        Assert.Equal(scene.Connections.Count, routes.Length);

        foreach (PolylineSceneElement route in routes)
        {
            AssertOrthogonal(route.Points);
        }

        GroupSceneElement frame =
            Assert.IsType<GroupSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value == "detail/B1"));

        PolylineSceneElement[] auxiliaryRoutes =
            routes
                .Where(route =>
                    route.LineStyleId is
                        "NEUTRAL_AUX" or
                        "GROUND_AUX")
                .ToArray();

        Assert.NotEmpty(auxiliaryRoutes);

        double frameClearance =
            Ric18BoardLayoutTokens
                .From(profile.Layout)
                .AuxiliaryFrameClearanceMm;
        MmRect safeArea =
            new(
                frame.Bounds.X + frameClearance,
                frame.Bounds.Y + frameClearance,
                frame.Bounds.Width - (frameClearance * 2.0),
                frame.Bounds.Height - (frameClearance * 2.0));

        Assert.All(
            auxiliaryRoutes,
            route =>
                Assert.All(
                    route.Points,
                    point =>
                        Assert.True(
                            safeArea.Contains(point),
                            $"Auxiliary route violated board-frame clearance: {route.Id} at {point}.")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(12)]
    public void GeneratedBoardDetail_MainBusTapsMatchCircuitColumnAxes(
        int circuitCount)
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineProjection projection =
            Project(
                GeneratedInput(
                    circuitCount,
                    reverse: false));
        SingleLineLayoutResult result =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics())
                .LayoutBoardDetail(
                    projection,
                    new EntityUid("B1"),
                    profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);
        DiagramScene scene =
            Assert.IsType<DiagramScene>(
                result.Scene);
        GroupSceneElement bus =
            Assert.IsType<GroupSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value ==
                    "detail/B1/bus/BUS:B1:MAIN"));

        for (int index = 1; index <= circuitCount; index++)
        {
            string suffix =
                index.ToString(
                    "D3",
                    System.Globalization.CultureInfo.InvariantCulture);
            GroupSceneElement branch =
                Assert.IsType<GroupSceneElement>(
                    scene.Elements.Single(element =>
                        element.Id.Value ==
                        $"detail/B1/branch/C{suffix}"));
            SceneAnchor tap =
                bus.Anchors.Single(anchor =>
                    anchor.Id == $"TAP:C{suffix}");
            SceneAnchor branchIn =
                branch.Anchors.Single(anchor =>
                    anchor.Id == "IN");
            SceneAnchor branchOut =
                branch.Anchors.Single(anchor =>
                    anchor.Id == "OUT");

            Assert.InRange(
                Math.Abs(
                    tap.Point.X -
                    branchIn.Point.X),
                0,
                1e-9);
            Assert.InRange(
                Math.Abs(
                    branchIn.Point.X -
                    branchOut.Point.X),
                0,
                1e-9);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void GeneratedBoardDetail_OddCircuitCountKeepsMiddleCircuitOnIncomingAxis(
        int circuitCount)
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineProjection projection =
            Project(
                GeneratedInput(
                    circuitCount,
                    reverse: false));
        SingleLineLayoutResult result =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics())
                .LayoutBoardDetail(
                    projection,
                    new EntityUid("B1"),
                    profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);
        DiagramScene scene =
            Assert.IsType<DiagramScene>(
                result.Scene);
        GroupSceneElement bus =
            Assert.IsType<GroupSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value ==
                    "detail/B1/bus/BUS:B1:MAIN"));

        SceneAnchor incoming =
            bus.Anchors.Single(anchor =>
                anchor.Id == "IN");
        int middleIndex =
            (circuitCount + 1) / 2;
        string suffix =
            middleIndex.ToString(
                "D3",
                System.Globalization.CultureInfo.InvariantCulture);
        SceneAnchor middleTap =
            bus.Anchors.Single(anchor =>
                anchor.Id == $"TAP:C{suffix}");

        Assert.InRange(
            Math.Abs(
                incoming.Point.X -
                middleTap.Point.X),
            0,
            1e-9);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void GeneratedBoardDetail_EvenCircuitCountStraddlesIncomingAxisSymmetrically(
        int circuitCount)
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineProjection projection =
            Project(
                GeneratedInput(
                    circuitCount,
                    reverse: false));
        SingleLineLayoutResult result =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics())
                .LayoutBoardDetail(
                    projection,
                    new EntityUid("B1"),
                    profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);
        DiagramScene scene =
            Assert.IsType<DiagramScene>(
                result.Scene);
        GroupSceneElement bus =
            Assert.IsType<GroupSceneElement>(
                scene.Elements.Single(element =>
                    element.Id.Value ==
                    "detail/B1/bus/BUS:B1:MAIN"));

        SceneAnchor incoming =
            bus.Anchors.Single(anchor =>
                anchor.Id == "IN");
        int leftIndex =
            circuitCount / 2;
        int rightIndex =
            leftIndex + 1;
        string leftSuffix =
            leftIndex.ToString(
                "D3",
                System.Globalization.CultureInfo.InvariantCulture);
        string rightSuffix =
            rightIndex.ToString(
                "D3",
                System.Globalization.CultureInfo.InvariantCulture);
        double leftX =
            bus.Anchors.Single(anchor =>
                anchor.Id == $"TAP:C{leftSuffix}").Point.X;
        double rightX =
            bus.Anchors.Single(anchor =>
                anchor.Id == $"TAP:C{rightSuffix}").Point.X;

        Assert.True(leftX < incoming.Point.X);
        Assert.True(rightX > incoming.Point.X);
        Assert.InRange(
            Math.Abs(
                (incoming.Point.X - leftX) -
                (rightX - incoming.Point.X)),
            0,
            1e-9);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(12)]
    [InlineData(48)]
    [InlineData(100)]
    public void EquivalentReorderedInput_ProducesSameSceneFingerprint(
        int circuitCount)
    {
        RIC18DrawingProfile profile = Profile();
        SingleLineProjection normal =
            Project(GeneratedInput(circuitCount, reverse: false));
        SingleLineProjection reversed =
            Project(GeneratedInput(circuitCount, reverse: true));
        var engine = new SingleLineLayoutEngine(new DeterministicTextMetrics());

        SingleLineLayoutResult first =
            engine.LayoutBoardDetail(
                normal,
                new EntityUid("B1"),
                profile);
        SingleLineLayoutResult second =
            engine.LayoutBoardDetail(
                reversed,
                new EntityUid("B1"),
                profile);

        Assert.True(
            first.Success,
            first.Failure?.Message);
        Assert.True(
            second.Success,
            second.Failure?.Message);

        Assert.Equal(
            normal.InputFingerprint,
            reversed.InputFingerprint);
        Assert.Equal(
            SingleLineProjectionFingerprint.Compute(normal),
            SingleLineProjectionFingerprint.Compute(reversed));
        Assert.Equal(
            first.SceneFingerprint,
            second.SceneFingerprint);
    }

    private static SingleLineInput GeneratedInput(
        int circuitCount,
        bool reverse)
    {
        var project = new ProjectInput(
            new EntityUid("P-STRESS"),
            "P-STRESS",
            $"Stress {circuitCount}",
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
        var board = new BoardInput(
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
        var supply = new SupplyConnection(
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

        var circuits = new List<CircuitInput>(circuitCount);
        var protections =
            new List<ProtectionInput>(circuitCount);

        for (int index = 1; index <= circuitCount; index++)
        {
            string suffix = index.ToString(
                "D3",
                System.Globalization.CultureInfo.InvariantCulture);
            var circuit = new CircuitInput(
                new EntityUid($"C{suffix}"),
                board.Uid,
                index,
                $"C{suffix}",
                $"Carga {suffix}",
                CircuitRole.Final,
                "CARGA",
                index % 3 == 0 ? "3F" : "1F",
                index % 3 == 0 ? 400m : 230m,
                1m,
                10m + index,
                null,
                null,
                new ConductorInput(
                    "CU",
                    "THHN",
                    2.5m,
                    2.5m,
                    index % 3 == 0 ? 4 : 2,
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
                    index % 3 == 0 ? 3 : 2,
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

        IEnumerable<CircuitInput> orderedCircuits =
            reverse
                ? circuits.AsEnumerable().Reverse()
                : circuits;
        IEnumerable<ProtectionInput> orderedProtections =
            reverse
                ? protections.AsEnumerable().Reverse()
                : protections;

        return new SingleLineInput(
            project,
            [source],
            [board],
            [],
            orderedCircuits,
            [supply],
            orderedProtections,
            [],
            [],
            new SingleLineInputMetadata(
                "1",
                "TEST",
                $"stress-{circuitCount}"));
    }

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

    private static bool FinitePositive(MmRect value) =>
        double.IsFinite(value.X) &&
        double.IsFinite(value.Y) &&
        double.IsFinite(value.Width) &&
        double.IsFinite(value.Height) &&
        value.Width > 0 &&
        value.Height > 0;

    private static void AssertOrthogonal(
        IReadOnlyList<MmPoint> points)
    {
        Assert.True(points.Count >= 2);

        for (int index = 0;
             index < points.Count - 1;
             index++)
        {
            MmPoint first = points[index];
            MmPoint second = points[index + 1];

            Assert.True(
                first.X == second.X ||
                first.Y == second.Y,
                $"Non-orthogonal route: {first} -> {second}");
        }
    }

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(
                AppContext.BaseDirectory,
                "ProfileData"));
}
