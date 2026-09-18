using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class SingleLineLayoutEngineTests
{
    [Fact]
    public void LayoutSummary_Minimal_ProducesValidTraceableScene()
    {
        SingleLineProjection projection =
            Project(SemanticFixtureFactory.Minimal());
        RIC18DrawingProfile profile = Profile();

        SingleLineLayoutResult result =
            Engine().LayoutSummary(
                projection,
                profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);
        DiagramScene scene =
            Assert.IsType<DiagramScene>(result.Scene);

        Assert.Equal(
            DrawingProfileFingerprint.Compute(profile),
            scene.Metadata.DrawingProfileFingerprint);
        Assert.Equal(
            projection.InputFingerprint,
            scene.Metadata.InputFingerprint);
        Assert.NotEqual(
            scene.Metadata.InputFingerprint,
            scene.Metadata.ProjectionFingerprint);
        Assert.Equal(64, scene.Metadata.ProjectionFingerprint.Length);
        Assert.NotNull(result.SceneFingerprint);
        Assert.Equal(
            DiagramSceneFingerprint.Compute(scene),
            result.SceneFingerprint);

        DiagramSceneValidationResult validation =
            new DiagramSceneValidator().Validate(
                scene,
                SceneValidationMode.Strict);
        Assert.False(
            validation.HasErrors,
            string.Join(Environment.NewLine, validation.Issues));
    }

    [Fact]
    public void LayoutBoardDetail_Nested_ProducesRoutedOrthogonalScene()
    {
        SingleLineProjection projection =
            Project(SemanticFixtureFactory.NestedBoards());
        RIC18DrawingProfile profile = Profile();

        SingleLineLayoutResult result =
            Engine().LayoutBoardDetail(
                projection,
                new EntityUid("B1"),
                profile);

        Assert.True(
            result.Success,
            result.Failure?.Message);
        DiagramScene scene =
            Assert.IsType<DiagramScene>(result.Scene);

        PolylineSceneElement[] routed = scene.Elements
            .OfType<PolylineSceneElement>()
            .Where(element =>
                element.Metadata.ContainsKey("connectionId"))
            .ToArray();

        Assert.Equal(scene.Connections.Count, routed.Length);
        Assert.NotEmpty(routed);

        foreach (PolylineSceneElement route in routed)
        {
            for (int index = 0;
                 index < route.Points.Count - 1;
                 index++)
            {
                MmPoint first = route.Points[index];
                MmPoint second = route.Points[index + 1];

                Assert.True(
                    first.X == second.X ||
                    first.Y == second.Y);
            }
        }
    }

    [Fact]
    public void Layout_InvalidProfile_ReturnsTypedFailureBeforeScene()
    {
        SingleLineProjection projection =
            Project(SemanticFixtureFactory.Minimal());
        RIC18DrawingProfile source = Profile();
        var invalid = new RIC18DrawingProfile(
            source.ProfileId,
            source.Version,
            source.SourceDocument,
            source.Provenance.Where(
                item => item.Id != source.Layout.ProvenanceId),
            source.Symbols,
            source.Blocks,
            source.LineStyles,
            source.TextStyles,
            source.Layout);

        SingleLineLayoutResult result =
            Engine().LayoutSummary(
                projection,
                invalid);

        Assert.False(result.Success);
        Assert.Null(result.Scene);
        Assert.NotNull(result.Failure);
        Assert.Equal(
            LayoutFailureStage.ProfileValidation,
            result.Failure.Stage);
        Assert.Contains(
            result.ProfileValidation.Issues,
            issue => issue.Code ==
                ProfileValidationCodes.MissingProvenance);
    }

    [Fact]
    public void Layout_RepeatedInputProfileAndState_IsDeterministic()
    {
        SingleLineProjection projection =
            Project(SemanticFixtureFactory.NestedBoards());
        RIC18DrawingProfile profile = Profile();
        var state = new DiagramLayoutState(
            DiagramSceneKind.ProjectSummary,
            projection.ProjectUid,
            "1",
            [],
            null);
        var engine = Engine();

        SingleLineLayoutResult first =
            engine.LayoutSummary(
                projection,
                profile,
                state);
        SingleLineLayoutResult second =
            engine.LayoutSummary(
                projection,
                profile,
                state);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(
            first.SceneFingerprint,
            second.SceneFingerprint);
        Assert.Equal(
            Assert.IsType<PositionedLayout>(first.Layout)
                .Blocks
                .Select(x => (x.BlockId, x.Bounds)),
            Assert.IsType<PositionedLayout>(second.Layout)
                .Blocks
                .Select(x => (x.BlockId, x.Bounds)));
    }

    [Fact]
    public void Layout_UsesInjectedTextMetricsAndCarriesSceneContract()
    {
        SingleLineProjection projection =
            Project(SemanticFixtureFactory.Minimal());
        RIC18DrawingProfile profile = Profile();
        var metrics = new CountingTextMetrics();

        SingleLineLayoutResult result =
            new SingleLineLayoutEngine(metrics).LayoutSummary(
                projection,
                profile);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.True(metrics.CallCount > 0);

        DiagramScene scene =
            Assert.IsType<DiagramScene>(result.Scene);
        Assert.Equal(
            new SceneId("summary/project/P1"),
            scene.Id);
        Assert.Equal(
            DiagramSceneKind.ProjectSummary,
            scene.Kind);
        Assert.Equal(
            projection.Issues.Count,
            scene.Issues.Count);
    }

    [Fact]
    public void Engine_RequiresExplicitTextMetricsDependency()
    {
        var constructors =
            typeof(SingleLineLayoutEngine).GetConstructors();

        Assert.Single(constructors);
        Assert.Equal(
            [typeof(ITextMetrics)],
            constructors[0]
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray());
    }

    [Fact]
    public void Layout_ProjectionWarningSurvivesIntoSceneIssues()
    {
        SingleLineInput source =
            SemanticFixtureFactory.Minimal();
        CircuitInput incomplete =
            source.Circuits[0] with
            {
                Conductor = null,
                DataState = DataState.Incomplete
            };
        var input = new SingleLineInput(
            source.Project,
            source.Sources,
            source.Boards,
            source.Buses,
            [incomplete],
            source.SupplyConnections,
            source.Protections,
            source.Grounding,
            source.Results,
            source.Metadata);
        SingleLineProjection projection =
            Project(input);

        SingleLineLayoutResult result =
            Engine().LayoutSummary(
                projection,
                Profile());

        Assert.True(result.Success, result.Failure?.Message);
        DiagramScene scene =
            Assert.IsType<DiagramScene>(result.Scene);
        SceneIssue issue = Assert.Single(
            scene.Issues,
            value => value.Code ==
                ValidationCodes.CircuitMissingConductor);

        Assert.Equal(SceneIssueSeverity.Warning, issue.Severity);
        Assert.Equal(
            new EntityReference(
                incomplete.Uid,
                EntityKind.Circuit),
            issue.Entity);
    }

    [Fact]
    public void ProjectionFingerprint_RepeatedProjection_IsStable()
    {
        SingleLineProjection projection =
            Project(SemanticFixtureFactory.NestedBoards());

        string first =
            SingleLineProjectionFingerprint.Compute(projection);
        string second =
            SingleLineProjectionFingerprint.Compute(projection);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.NotEqual(projection.InputFingerprint, first);
    }

    private static SingleLineProjection Project(SingleLineInput input)
    {
        ProjectionBuildResult result =
            new SingleLineProjectionBuilder().Build(input);
        Assert.True(
            result.Success,
            string.Join(Environment.NewLine, result.Validation.Issues));
        return Assert.IsType<SingleLineProjection>(
            result.Projection);
    }

    private static SingleLineLayoutEngine Engine() =>
        new(new DeterministicTextMetrics());

    private sealed class CountingTextMetrics : ITextMetrics
    {
        public int CallCount { get; private set; }

        public TextMeasurement Measure(
            string text,
            TextStyleDefinition style)
        {
            CallCount++;
            return new TextMeasurement(
                Math.Max(style.HeightMm, text.Length * style.HeightMm),
                style.HeightMm);
        }
    }

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));
}
