using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
        _viewModel.UndoLayout();
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnRedoLayoutClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.RedoLayout();
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnPinSelectedClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.SetSelectedLayoutLockMode(
            LayoutLockMode.Pinned);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnLockSelectedClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.SetSelectedLayoutLockMode(
            LayoutLockMode.Locked);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnResetSelectedClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.ResetSelectedLayout();
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnSelectionChanged(
        object? sender,
        InteractionSelectionChangedEventArgs e)
    {
        _viewModel.ApplySelection(
            e.Intent);
        UpdateInteractionShell();
    }

    private void OnLayoutMoveRequested(
        object? sender,
        LayoutMoveRequestedEventArgs e)
    {
        _viewModel.ApplyLayoutMove(
            e.Intent);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnElectricalProposalRequested(
        object? sender,
        ElectricalProposalRequestedEventArgs e)
    {
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
        await _viewModel.ExecutePendingElectricalAsync(
            confirmationGranted: false);
        ApplyWorkspace(
            fitScene: false);
    }

    private async void OnConfirmElectricalClicked(
        object? sender,
        RoutedEventArgs e)
    {
        await _viewModel.ExecutePendingElectricalAsync(
            confirmationGranted: true);
        ApplyWorkspace(
            fitScene: false);
    }

    private void OnCancelElectricalClicked(
        object? sender,
        RoutedEventArgs e)
    {
        _viewModel.CancelElectricalProposal();
        UpdateInteractionShell();
    }

    private void SetInteractionMode(
        InteractionMode mode)
    {
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

    private void ApplyWorkspace(
        bool fitScene)
    {
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
            $"Mode: {_viewModel.InteractionMode}";
        InteractionStateText.Text =
            $"State: {DiagramView.InteractionState.Kind}";

        SelectionText.Text =
            _viewModel.SelectedEntity is null
                ? "Selection: —"
                : $"Selection: {_viewModel.SelectedEntity.Kind} {_viewModel.SelectedEntity.Uid.Value}";

        LayoutModeButton.IsEnabled =
            _viewModel.Capabilities.CanEditLayout;
        ElectricalModeButton.IsEnabled =
            _viewModel.Capabilities.CanEditElectrical;
        UndoLayoutButton.IsEnabled =
            _viewModel.CanUndoLayout;
        RedoLayoutButton.IsEnabled =
            _viewModel.CanRedoLayout;

        bool selectedLayoutEditable =
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
            $"Host capabilities — Layout: {EnabledText(_viewModel.Capabilities.CanEditLayout)}, " +
            $"Electrical: {EnabledText(_viewModel.Capabilities.CanEditElectrical)}";

        ElectricalCommandProposal? proposal =
            _viewModel.PendingElectricalProposal;
        CommandResult? result =
            _viewModel.LastElectricalResult;

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
