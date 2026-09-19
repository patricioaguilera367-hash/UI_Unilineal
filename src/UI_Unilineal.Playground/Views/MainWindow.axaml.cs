using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Interaction;
using UI_Unilineal.Engine.Interaction.Electrical;
using UI_Unilineal.Engine.Interaction.Layout;
using UI_Unilineal.Playground.Fixtures;
using UI_Unilineal.Playground.ViewModels;
using UI_Unilineal.Rendering.Avalonia.Interaction;

namespace UI_Unilineal.Playground.Views;

public sealed partial class MainWindow : Window
{
    private readonly SingleLineWorkspaceViewModel _viewModel;
    private bool _showingSymbolGallery;
    private bool _galleryShowGrid = true;
    private bool _galleryShowBounds = true;
    private bool _galleryShowAnchors;

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
        if (_showingSymbolGallery)
        {
            _viewModel.ShowSummary();
            ApplyWorkspace(
                fitScene: true);
            return;
        }

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

    private void OnSymbolsClicked(
        object? sender,
        RoutedEventArgs e) =>
        ShowSymbolGallery();

    private void OnGalleryGridClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_showingSymbolGallery)
        {
            return;
        }

        _galleryShowGrid = !_galleryShowGrid;
        RefreshSymbolGallery(
            fitScene: false);
    }

    private void OnGalleryBoundsClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_showingSymbolGallery)
        {
            return;
        }

        _galleryShowBounds = !_galleryShowBounds;
        RefreshSymbolGallery(
            fitScene: false);
    }

    private void OnGalleryAnchorsClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (!_showingSymbolGallery)
        {
            return;
        }

        _galleryShowAnchors = !_galleryShowAnchors;
        RefreshSymbolGallery(
            fitScene: false);
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

    private void OnNavigateModeClicked(
        object? sender,
        RoutedEventArgs e) =>
        SetInteractionMode(
            InteractionMode.Navigate);

    private void OnLayoutModeClicked(
        object? sender,
        RoutedEventArgs e) =>
        SetInteractionMode(
            InteractionMode.Layout);

    private void OnElectricalModeClicked(
        object? sender,
        RoutedEventArgs e) =>
        SetInteractionMode(
            InteractionMode.Electrical);

    private void OnUndoLayoutClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.UndoLayout();
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnRedoLayoutClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.RedoLayout();
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnPinSelectedClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.SetSelectedLayoutLockMode(
            LayoutLockMode.Pinned);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnLockSelectedClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.SetSelectedLayoutLockMode(
            LayoutLockMode.Locked);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnResetSelectedClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.ResetSelectedLayout();
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnSelectionChanged(
        object? sender,
        InteractionSelectionChangedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.ApplySelection(
            e.Intent);
        UpdateInteractionShell();
    }

    private void OnLayoutMoveRequested(
        object? sender,
        LayoutMoveRequestedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.ApplyLayoutMove(
            e.Intent);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnElectricalProposalRequested(
        object? sender,
        ElectricalProposalRequestedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.CreateElectricalProposal(
            e.Intent);
        UpdateInteractionShell();
    }

    private void OnInteractionStateChanged(
        object? sender,
        InteractionStateChangedEventArgs e)
    {
        InteractionStateText.Text =
            $"State: {e.Current.Kind}";
    }

    private async void OnApplyElectricalClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        await _viewModel.ExecutePendingElectricalAsync(
            confirmationGranted: false);
        ApplyWorkspace(
            fitScene: false);
    }

    private async void OnConfirmElectricalClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        await _viewModel.ExecutePendingElectricalAsync(
            confirmationGranted: true);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnCancelElectricalClicked(
        object? sender,
        RoutedEventArgs e)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        _viewModel.CancelElectricalProposal();
        UpdateInteractionShell();
    }

    private void SetInteractionMode(
        InteractionMode mode)
    {
        if (_showingSymbolGallery)
        {
            return;
        }

        if (!_viewModel.TrySetInteractionMode(mode))
        {
            UpdateInteractionShell();
            return;
        }

        DiagramView.InteractionMode =
            _viewModel.InteractionMode;
        UpdateInteractionShell();
    }

    private void OpenBoard(
        EntityUid boardUid)
    {
        _viewModel.OpenBoard(boardUid);
        ApplyWorkspace(
            fitScene: true);
    }

    private void ShowSymbolGallery()
    {
        _showingSymbolGallery = true;

        _viewModel.TrySetInteractionMode(
            InteractionMode.Navigate);
        _viewModel.CancelElectricalProposal();

        DiagramView.DrawingProfile =
            _viewModel.Profile;
        DiagramView.InteractionMode =
            InteractionMode.Navigate;

        BackButton.IsEnabled = true;
        RouteText.Text = "RIC18 Symbol Gallery";

        RefreshSymbolGallery(
            fitScene: true);
    }

    private void RefreshSymbolGallery(
        bool fitScene)
    {
        DiagramView.Scene =
            SymbolGallerySceneBuilder.Build(
                _viewModel.Profile,
                _galleryShowGrid,
                _galleryShowBounds,
                _galleryShowAnchors);

        UpdateInteractionShell();

        if (fitScene)
        {
            Dispatcher.UIThread.Post(
                DiagramView.FitScene);
        }
    }

    private void ApplyWorkspace(
        bool fitScene)
    {
        _showingSymbolGallery = false;

        DiagramView.DrawingProfile =
            _viewModel.Profile;
        DiagramView.InteractionMode =
            _viewModel.InteractionMode;
        DiagramView.Scene =
            _viewModel.Scene;

        BackButton.IsEnabled =
            _viewModel.CanGoBack;
        RouteText.Text =
            _viewModel.CurrentRoute.Kind ==
            PlaygroundRouteKind.ProjectSummary
                ? "Project Summary"
                : $"Board {_viewModel.CurrentRoute.BoardUid?.Value}";

        UpdateInteractionShell();

        if (fitScene)
        {
            Dispatcher.UIThread.Post(
                DiagramView.FitScene);
        }
    }

    private void UpdateInteractionShell()
    {
        ModeText.Text =
            _showingSymbolGallery
                ? "Mode: Symbol review (read-only)"
                : $"Mode: {_viewModel.InteractionMode}";
        InteractionStateText.Text =
            $"State: {DiagramView.InteractionState.Kind}";

        SelectionText.Text =
            _showingSymbolGallery
                ? "Selection: gallery inspection only"
                : _viewModel.SelectedEntity is null
                    ? "Selection: —"
                    : $"Selection: {_viewModel.SelectedEntity.Kind} {_viewModel.SelectedEntity.Uid.Value}";

        LayoutModeButton.IsEnabled =
            !_showingSymbolGallery &&
            _viewModel.Capabilities.CanEditLayout;
        ElectricalModeButton.IsEnabled =
            !_showingSymbolGallery &&
            _viewModel.Capabilities.CanEditElectrical;

        GalleryGridButton.IsEnabled =
            _showingSymbolGallery;
        GalleryBoundsButton.IsEnabled =
            _showingSymbolGallery;
        GalleryAnchorsButton.IsEnabled =
            _showingSymbolGallery;
        GalleryGridButton.Content =
            _galleryShowGrid
                ? "Grid: On"
                : "Grid: Off";
        GalleryBoundsButton.Content =
            _galleryShowBounds
                ? "Bounds: On"
                : "Bounds: Off";
        GalleryAnchorsButton.Content =
            _galleryShowAnchors
                ? "Anchors: On"
                : "Anchors: Off";
        UndoLayoutButton.IsEnabled =
            !_showingSymbolGallery &&
            _viewModel.CanUndoLayout;
        RedoLayoutButton.IsEnabled =
            !_showingSymbolGallery &&
            _viewModel.CanRedoLayout;

        bool selectedLayoutEditable =
            !_showingSymbolGallery &&
            _viewModel.Capabilities.CanEditLayout &&
            _viewModel.InteractionMode ==
            InteractionMode.Layout &&
            _viewModel.SelectedEntity is not null;

        PinSelectedButton.IsEnabled =
            selectedLayoutEditable;
        LockSelectedButton.IsEnabled =
            selectedLayoutEditable;
        ResetSelectedButton.IsEnabled =
            selectedLayoutEditable;

        CapabilitiesText.Text =
            _showingSymbolGallery
                ? "Gallery — read-only review. Grid, nominal bounds/guides and anchor circles are optional inspection overlays."
                : $"Host capabilities — Layout: {EnabledText(_viewModel.Capabilities.CanEditLayout)}, " +
                  $"Electrical: {EnabledText(_viewModel.Capabilities.CanEditElectrical)}";

        ElectricalCommandProposal? proposal =
            _showingSymbolGallery
                ? null
                : _viewModel.PendingElectricalProposal;
        CommandResult? result =
            _showingSymbolGallery
                ? null
                : _viewModel.LastElectricalResult;

        ElectricalPreviewPanel.IsVisible =
            proposal is not null ||
            result is not null;

        ElectricalProposalText.Text =
            proposal is null
                ? "No pending electrical proposal."
                : $"{proposal.Command.GetType().Name} · {proposal.Impact}" +
                  (proposal.RequiresConfirmation
                      ? " · confirmation required"
                      : string.Empty);

        ElectricalStatusText.Text =
            FormatResult(result);

        ApplyElectricalButton.IsEnabled =
            proposal is not null &&
            _viewModel.Capabilities.CanEditElectrical;
        ConfirmElectricalButton.IsEnabled =
            proposal is not null &&
            proposal.RequiresConfirmation &&
            _viewModel.Capabilities.CanEditElectrical;
        CancelElectricalButton.IsEnabled =
            proposal is not null;
    }

    private static string EnabledText(
        bool value) =>
        value
            ? "enabled"
            : "read-only";

    private static string FormatResult(
        CommandResult? result)
    {
        if (result is null)
        {
            return "Result: —";
        }

        string diagnostics =
            result.Diagnostics.Count == 0
                ? string.Empty
                : $" · {string.Join(" | ", result.Diagnostics)}";

        return $"Result: {result.Status}{diagnostics}";
    }
}
