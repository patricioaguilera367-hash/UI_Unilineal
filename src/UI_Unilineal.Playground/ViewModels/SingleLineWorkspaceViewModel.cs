using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Playground.Fixtures;

namespace UI_Unilineal.Playground.ViewModels;

public enum PlaygroundRouteKind
{
    ProjectSummary,
    BoardDetail
}

public sealed record PlaygroundRoute(
    PlaygroundRouteKind Kind,
    EntityUid? BoardUid = null);

public sealed class SingleLineWorkspaceViewModel
{
    private readonly SingleLineProjection _projection;
    private readonly SingleLineLayoutEngine _layoutEngine;
    private readonly List<PlaygroundRoute> _history = [];

    public SingleLineWorkspaceViewModel(
        PlaygroundFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _projection = fixture.Projection;
        Profile = fixture.Profile;
        _layoutEngine =
            new SingleLineLayoutEngine(
                new DeterministicTextMetrics());

        var initialRoute =
            new PlaygroundRoute(
                PlaygroundRouteKind.ProjectSummary);

        _history.Add(initialRoute);
        Scene = BuildScene(initialRoute);
    }

    public DiagramScene Scene { get; private set; }

    public RIC18DrawingProfile Profile { get; }

    public PlaygroundRoute CurrentRoute =>
        _history[^1];

    public bool CanGoBack =>
        _history.Count > 1;

    public void ShowSummary() =>
        Navigate(
            new PlaygroundRoute(
                PlaygroundRouteKind.ProjectSummary));

    public void OpenBoard(
        EntityUid boardUid)
    {
        ArgumentNullException.ThrowIfNull(boardUid);

        if (!_projection.BoardDetails.Any(
                detail => detail.Board.Uid == boardUid))
        {
            throw new ArgumentException(
                $"Board '{boardUid.Value}' does not exist in the Playground projection.",
                nameof(boardUid));
        }

        Navigate(
            new PlaygroundRoute(
                PlaygroundRouteKind.BoardDetail,
                boardUid));
    }

    public void Back()
    {
        if (!CanGoBack)
        {
            return;
        }

        PlaygroundRoute target =
            _history[^2];
        DiagramScene scene =
            BuildScene(target);

        _history.RemoveAt(
            _history.Count - 1);
        Scene = scene;
    }

    private void Navigate(
        PlaygroundRoute route)
    {
        if (route == CurrentRoute)
        {
            return;
        }

        DiagramScene scene =
            BuildScene(route);

        _history.Add(route);
        Scene = scene;
    }

    private DiagramScene BuildScene(
        PlaygroundRoute route)
    {
        SingleLineLayoutResult result =
            route.Kind switch
            {
                PlaygroundRouteKind.ProjectSummary =>
                    _layoutEngine.LayoutSummary(
                        _projection,
                        Profile),
                PlaygroundRouteKind.BoardDetail
                    when route.BoardUid is not null =>
                    _layoutEngine.LayoutBoardDetail(
                        _projection,
                        route.BoardUid,
                        Profile),
                _ => throw new InvalidOperationException(
                    "The Playground route is incomplete.")
            };

        if (!result.Success ||
            result.Scene is null)
        {
            string detail =
                result.Failure?.Message ??
                "The layout engine returned no scene.";

            throw new InvalidOperationException(
                $"Could not build Playground scene: {detail}");
        }

        return result.Scene;
    }
}
