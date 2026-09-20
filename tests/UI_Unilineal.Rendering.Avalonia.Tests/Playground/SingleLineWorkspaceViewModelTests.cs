using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Playground.ViewModels;

namespace UI_Unilineal.Rendering.Avalonia.Tests.Playground;

public sealed class SingleLineWorkspaceViewModelTests
{
    [Fact]
    public void NavigationStack_RestoresNestedRoutesDeterministically()
    {
        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create());

        Assert.Equal(
            PlaygroundRouteKind.ProjectSummary,
            viewModel.CurrentRoute.Kind);
        Assert.Null(viewModel.CurrentRoute.BoardUid);
        Assert.Equal(
            DiagramSceneKind.ProjectSummary,
            viewModel.Scene.Kind);
        Assert.False(viewModel.CanGoBack);

        viewModel.OpenBoard(new EntityUid("B1"));

        Assert.Equal(
            PlaygroundRouteKind.BoardDetail,
            viewModel.CurrentRoute.Kind);
        Assert.Equal(
            new EntityUid("B1"),
            viewModel.CurrentRoute.BoardUid);
        Assert.Equal(
            DiagramSceneKind.BoardDetail,
            viewModel.Scene.Kind);
        Assert.True(viewModel.CanGoBack);

        viewModel.OpenBoard(new EntityUid("B2"));

        Assert.Equal(
            new EntityUid("B2"),
            viewModel.CurrentRoute.BoardUid);
        Assert.True(viewModel.CanGoBack);

        viewModel.Back();

        Assert.Equal(
            PlaygroundRouteKind.BoardDetail,
            viewModel.CurrentRoute.Kind);
        Assert.Equal(
            new EntityUid("B1"),
            viewModel.CurrentRoute.BoardUid);
        Assert.True(viewModel.CanGoBack);

        viewModel.Back();

        Assert.Equal(
            PlaygroundRouteKind.ProjectSummary,
            viewModel.CurrentRoute.Kind);
        Assert.Null(viewModel.CurrentRoute.BoardUid);
        Assert.Equal(
            DiagramSceneKind.ProjectSummary,
            viewModel.Scene.Kind);
        Assert.False(viewModel.CanGoBack);
    }

    [Fact]
    public void ShowSummary_AlwaysReturnsToCanonicalRoot()
    {
        var viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create());

        viewModel.OpenBoard(new EntityUid("B1"));
        viewModel.OpenBoard(new EntityUid("B2"));
        viewModel.ShowSummary();

        Assert.Equal(
            PlaygroundRouteKind.ProjectSummary,
            viewModel.CurrentRoute.Kind);
        Assert.Null(viewModel.CurrentRoute.BoardUid);
        Assert.Equal(
            DiagramSceneKind.ProjectSummary,
            viewModel.Scene.Kind);
        Assert.False(viewModel.CanGoBack);

        viewModel.OpenBoard(new EntityUid("B1"));
        viewModel.Back();

        Assert.Equal(
            PlaygroundRouteKind.ProjectSummary,
            viewModel.CurrentRoute.Kind);
        Assert.False(viewModel.CanGoBack);

        viewModel.OpenBoard(new EntityUid("B2"));
        viewModel.ShowSummary();

        Assert.Equal(
            PlaygroundRouteKind.ProjectSummary,
            viewModel.CurrentRoute.Kind);
        Assert.False(viewModel.CanGoBack);
    }
}
