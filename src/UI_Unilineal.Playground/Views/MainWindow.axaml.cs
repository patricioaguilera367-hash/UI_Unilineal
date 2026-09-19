using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Playground.ViewModels;

namespace UI_Unilineal.Playground.Views;

public sealed partial class MainWindow : Window
{
    private readonly SingleLineWorkspaceViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel =
            new SingleLineWorkspaceViewModel(
                PlaygroundFixtureFactory.Create());

        ApplyWorkspace(
            fitScene: true);
    }

    private void OnBackClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.Back();
        ApplyWorkspace(
            fitScene: true);
    }

    private void OnSummaryClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.ShowSummary();
        ApplyWorkspace(
            fitScene: true);
    }

    private void OnBoardB1Clicked(
        object? sender,
        RoutedEventArgs e) =>
        OpenBoard(
            new EntityUid("B1"));

    private void OnBoardB2Clicked(
        object? sender,
        RoutedEventArgs e) =>
        OpenBoard(
            new EntityUid("B2"));

    private void OnZoomOutClicked(
        object? sender,
        RoutedEventArgs e) =>
        DiagramView.ZoomOut();

    private void OnZoomInClicked(
        object? sender,
        RoutedEventArgs e) =>
        DiagramView.ZoomIn();

    private void OnFitClicked(
        object? sender,
        RoutedEventArgs e) =>
        DiagramView.FitScene();

    private void OpenBoard(
        EntityUid boardUid)
    {
        _viewModel.OpenBoard(boardUid);
        ApplyWorkspace(
            fitScene: true);
    }

    private void ApplyWorkspace(
        bool fitScene)
    {
        DiagramView.DrawingProfile =
            _viewModel.Profile;
        DiagramView.Scene =
            _viewModel.Scene;

        BackButton.IsEnabled =
            _viewModel.CanGoBack;
        RouteText.Text =
            _viewModel.CurrentRoute.Kind ==
            PlaygroundRouteKind.ProjectSummary
                ? "Project Summary"
                : $"Board {_viewModel.CurrentRoute.BoardUid?.Value}";

        if (fitScene)
        {
            Dispatcher.UIThread.Post(
                DiagramView.FitScene);
        }
    }
}
