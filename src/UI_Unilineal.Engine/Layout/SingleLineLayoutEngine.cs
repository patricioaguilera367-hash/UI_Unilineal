using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Layout;

public enum LayoutFailureStage
{
    ProfileValidation,
    Composition,
    Measurement,
    Placement,
    Assembly,
    Routing,
    SceneValidation
}

public sealed record LayoutEngineFailure(
    LayoutFailureStage Stage,
    string Code,
    string Message);

public sealed class SingleLineLayoutResult
{
    private SingleLineLayoutResult(
        DiagramScene? scene,
        PositionedLayout? layout,
        DrawingProfileValidationResult profileValidation,
        DiagramSceneValidationResult? sceneValidation,
        LayoutEngineFailure? failure,
        string? sceneFingerprint)
    {
        Scene = scene;
        Layout = layout;
        ProfileValidation = profileValidation;
        SceneValidation = sceneValidation;
        Failure = failure;
        SceneFingerprint = sceneFingerprint;
    }

    public DiagramScene? Scene { get; }

    public PositionedLayout? Layout { get; }

    public DrawingProfileValidationResult ProfileValidation { get; }

    public DiagramSceneValidationResult? SceneValidation { get; }

    public LayoutEngineFailure? Failure { get; }

    public string? SceneFingerprint { get; }

    public bool Success =>
        Scene is not null &&
        Layout is not null &&
        Failure is null &&
        !ProfileValidation.HasErrors &&
        SceneValidation is { HasErrors: false };

    public static SingleLineLayoutResult Failed(
        DrawingProfileValidationResult profileValidation,
        LayoutEngineFailure failure,
        PositionedLayout? layout = null,
        DiagramSceneValidationResult? sceneValidation = null) =>
        new(
            null,
            layout,
            profileValidation,
            sceneValidation,
            failure,
            null);

    public static SingleLineLayoutResult Succeeded(
        DiagramScene scene,
        PositionedLayout layout,
        DrawingProfileValidationResult profileValidation,
        DiagramSceneValidationResult sceneValidation,
        string sceneFingerprint) =>
        new(
            scene,
            layout,
            profileValidation,
            sceneValidation,
            null,
            sceneFingerprint);
}

public sealed class SingleLineLayoutEngine
{
    public const string LayoutEngineVersion = "G5-1";

    private readonly ITextMetrics _textMetrics;

    public SingleLineLayoutEngine(ITextMetrics textMetrics)
    {
        _textMetrics = textMetrics ??
            throw new ArgumentNullException(nameof(textMetrics));
    }

    public SingleLineLayoutResult LayoutSummary(
        SingleLineProjection projection,
        RIC18DrawingProfile profile,
        DiagramLayoutState? layoutState = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(profile);

        return Layout(
            projection,
            profile,
            layoutState,
            expectedSceneKind: DiagramSceneKind.ProjectSummary,
            scopeUid: projection.ProjectUid,
            buildComposition: () =>
                new CompositionBuilder()
                    .BuildSummary(projection, profile),
            strategy: new SummaryLayoutStrategy());
    }

    public SingleLineLayoutResult LayoutBoardDetail(
        SingleLineProjection projection,
        EntityUid boardUid,
        RIC18DrawingProfile profile,
        DiagramLayoutState? layoutState = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(boardUid);
        ArgumentNullException.ThrowIfNull(profile);

        return Layout(
            projection,
            profile,
            layoutState,
            expectedSceneKind: DiagramSceneKind.BoardDetail,
            scopeUid: boardUid,
            buildComposition: () =>
                new CompositionBuilder()
                    .BuildBoardDetail(
                        projection,
                        boardUid,
                        profile),
            strategy: new BoardDetailLayoutStrategy());
    }

    private SingleLineLayoutResult Layout(
        SingleLineProjection projection,
        RIC18DrawingProfile profile,
        DiagramLayoutState? layoutState,
        DiagramSceneKind expectedSceneKind,
        EntityUid scopeUid,
        Func<DrawingComposition> buildComposition,
        ISingleLineLayoutStrategy strategy)
    {
        DrawingProfileValidationResult profileValidation =
            new DrawingProfileValidator().Validate(profile);

        if (profileValidation.HasErrors)
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                new LayoutEngineFailure(
                    LayoutFailureStage.ProfileValidation,
                    "PROFILE_INVALID",
                    "Drawing profile contains validation errors."));
        }

        if (layoutState is not null)
        {
            if (layoutState.SceneKind != expectedSceneKind ||
                layoutState.ScopeUid != scopeUid)
            {
                return SingleLineLayoutResult.Failed(
                    profileValidation,
                    new LayoutEngineFailure(
                        LayoutFailureStage.Placement,
                        "LAYOUT_STATE_SCOPE_MISMATCH",
                        "Layout state does not match the requested scene scope."));
            }
        }

        DrawingComposition composition;

        try
        {
            composition = buildComposition();
        }
        catch (Exception exception) when (IsExpectedPipelineException(exception))
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                Failure(
                    LayoutFailureStage.Composition,
                    "COMPOSITION_FAILED",
                    exception));
        }

        CompositionMeasurement measurement;

        try
        {
            measurement = new CompositionMeasurer(
                _textMetrics)
                .Measure(composition, profile);
        }
        catch (Exception exception) when (IsExpectedPipelineException(exception))
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                Failure(
                    LayoutFailureStage.Measurement,
                    "MEASUREMENT_FAILED",
                    exception));
        }

        PositionedLayout positioned;

        try
        {
            positioned = strategy.Layout(
                composition,
                measurement,
                profile.Layout);

            if (layoutState is not null)
            {
                positioned = new LayoutOverrideApplicator().Apply(
                    positioned,
                    composition,
                    layoutState,
                    profile.Layout);
            }
        }
        catch (Exception exception) when (IsExpectedPipelineException(exception))
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                Failure(
                    LayoutFailureStage.Placement,
                    "PLACEMENT_FAILED",
                    exception));
        }

        string projectionFingerprint =
            SingleLineProjectionFingerprint.Compute(projection);
        SceneId sceneId = CreateSceneId(
            expectedSceneKind,
            scopeUid);
        SceneIssue[] sceneIssues = projection.Issues
            .OrderBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(
                issue => issue.Entity?.Uid.Value,
                StringComparer.Ordinal)
            .ThenBy(issue => issue.Field, StringComparer.Ordinal)
            .Select(ToSceneIssue)
            .ToArray();

        DiagramScene assembled;

        try
        {
            assembled = new SceneAssembly().Assemble(
                new SceneAssemblyInput(
                    composition,
                    positioned.Blocks,
                    positioned.Bounds,
                    projectionFingerprint,
                    LayoutEngineVersion,
                    sceneId,
                    expectedSceneKind,
                    sceneIssues),
                profile,
                measurement);
        }
        catch (Exception exception) when (IsExpectedPipelineException(exception))
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                Failure(
                    LayoutFailureStage.Assembly,
                    "ASSEMBLY_FAILED",
                    exception),
                positioned);
        }

        DiagramScene routed;

        try
        {
            routed = AddRoutes(
                assembled,
                profile.Layout);
        }
        catch (Exception exception) when (IsExpectedPipelineException(exception))
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                Failure(
                    LayoutFailureStage.Routing,
                    "ROUTING_FAILED",
                    exception),
                positioned);
        }

        DiagramSceneValidationResult sceneValidation =
            new DiagramSceneValidator().Validate(
                routed,
                SceneValidationMode.Strict);

        if (sceneValidation.HasErrors)
        {
            return SingleLineLayoutResult.Failed(
                profileValidation,
                new LayoutEngineFailure(
                    LayoutFailureStage.SceneValidation,
                    "SCENE_INVALID",
                    "Generated scene contains structural validation errors."),
                positioned,
                sceneValidation);
        }

        string sceneFingerprint =
            DiagramSceneFingerprint.Compute(routed);

        return SingleLineLayoutResult.Succeeded(
            routed,
            positioned,
            profileValidation,
            sceneValidation,
            sceneFingerprint);
    }

    private static DiagramScene AddRoutes(
        DiagramScene scene,
        LayoutProfile profile)
    {
        var elements = scene.Elements.ToList();
        var routeElements = new List<PolylineSceneElement>();
        var router = new OrthogonalConnectionRouter();

        foreach (SceneConnection connection in scene.Connections
                     .OrderBy(
                         value => value.Id.Value,
                         StringComparer.Ordinal))
        {
            RoutedConnection route;

            try
            {
                route = router.Route(
                    connection,
                    scene,
                    profile);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(
                    $"Routing failed for connection '{connection.Id.Value}' ({connection.LineStyleId}): {exception.Message}",
                    exception);
            }

            MmRect bounds = BoundsFor(route.Points);

            routeElements.Add(
                new PolylineSceneElement(
                    new SceneId(
                        $"{connection.Id.Value}/route"),
                    bounds,
                    connection.Layer,
                    connection.ZIndex,
                    connection.Visibility,
                    connection.SemanticReference,
                    new Dictionary<string, string>(
                        StringComparer.Ordinal)
                    {
                        ["connectionId"] =
                            connection.Id.Value
                    },
                    route.Points,
                    connection.LineStyleId));
        }

        elements.AddRange(routeElements);

        MmRect finalBounds = ExpandBounds(
            scene.Bounds,
            routeElements.Select(element => element.Bounds));

        return new DiagramScene(
            scene.Id,
            scene.Kind,
            finalBounds,
            elements,
            scene.Metadata,
            scene.Connections,
            scene.Issues);
    }

    private static MmRect BoundsFor(
        IReadOnlyList<MmPoint> points)
    {
        const double minimumExtent = 0.001;

        double minX = points.Min(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxX = points.Max(point => point.X);
        double maxY = points.Max(point => point.Y);

        return new MmRect(
            minX,
            minY,
            Math.Max(maxX - minX, minimumExtent),
            Math.Max(maxY - minY, minimumExtent));
    }

    private static MmRect ExpandBounds(
        MmRect current,
        IEnumerable<MmRect> additional)
    {
        MmRect[] values = additional.ToArray();

        if (values.Length == 0)
        {
            return current;
        }

        double minX = Math.Min(
            current.X,
            values.Min(value => value.X));
        double minY = Math.Min(
            current.Y,
            values.Min(value => value.Y));
        double maxRight = Math.Max(
            current.Right,
            values.Max(value => value.Right));
        double maxBottom = Math.Max(
            current.Bottom,
            values.Max(value => value.Bottom));

        return new MmRect(
            minX,
            minY,
            maxRight - minX,
            maxBottom - minY);
    }

    private static SceneId CreateSceneId(
        DiagramSceneKind kind,
        EntityUid scopeUid) =>
        kind switch
        {
            DiagramSceneKind.ProjectSummary =>
                new SceneId($"summary/project/{scopeUid.Value}"),
            DiagramSceneKind.BoardDetail =>
                new SceneId($"detail/board/{scopeUid.Value}"),
            _ => throw new InvalidOperationException(
                $"Unsupported scene kind '{kind}'.")
        };

    private static SceneIssue ToSceneIssue(
        ProjectionIssue issue) =>
        new(
            issue.Code,
            issue.Severity == ValidationSeverity.Error
                ? SceneIssueSeverity.Error
                : SceneIssueSeverity.Warning,
            issue.Message,
            issue.Entity,
            issue.Field);

    private static bool IsExpectedPipelineException(
        Exception exception) =>
        exception is InvalidOperationException or
            ArgumentException or
            KeyNotFoundException;

    private static LayoutEngineFailure Failure(
        LayoutFailureStage stage,
        string code,
        Exception exception) =>
        new(
            stage,
            code,
            exception.Message);
}
