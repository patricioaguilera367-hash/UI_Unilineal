using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Electrical;

public sealed class CommandResult
{
    private CommandResult(
        CommandResultStatus status,
        SingleLineInput? newInput,
        string? newRevision,
        IElectricalCommand? inverseCommand,
        IReadOnlyList<EntityReference> changedEntities,
        IReadOnlyList<EntityReference> invalidatedEntities,
        IReadOnlyList<string> diagnostics)
    {
        Status = status;
        NewInput = newInput;
        NewRevision = newRevision;
        InverseCommand = inverseCommand;
        ChangedEntities = changedEntities;
        InvalidatedEntities = invalidatedEntities;
        Diagnostics = diagnostics;
    }

    public CommandResultStatus Status { get; }

    public SingleLineInput? NewInput { get; }

    public string? NewRevision { get; }

    public IElectricalCommand? InverseCommand { get; }

    public IReadOnlyList<EntityReference> ChangedEntities { get; }

    public IReadOnlyList<EntityReference> InvalidatedEntities { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public static CommandResult Applied(
        SingleLineInput newInput,
        string newRevision,
        IElectricalCommand inverseCommand,
        IEnumerable<EntityReference>? changedEntities = null,
        IEnumerable<EntityReference>? invalidatedEntities = null,
        IEnumerable<string>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(newInput);
        ArgumentNullException.ThrowIfNull(inverseCommand);

        if (string.IsNullOrWhiteSpace(newRevision))
        {
            throw new ArgumentException(
                "New revision is required for an applied result.",
                nameof(newRevision));
        }

        return new CommandResult(
            CommandResultStatus.Applied,
            newInput,
            newRevision,
            inverseCommand,
            Copy(
                changedEntities),
            Copy(
                invalidatedEntities),
            CopyDiagnostics(
                diagnostics));
    }

    public static CommandResult Rejected(
        IEnumerable<string>? diagnostics = null) =>
        NotApplied(
            CommandResultStatus.Rejected,
            diagnostics);

    public static CommandResult NeedsConfirmation(
        IEnumerable<string>? diagnostics = null) =>
        NotApplied(
            CommandResultStatus.NeedsConfirmation,
            diagnostics);

    public static CommandResult Conflict(
        IEnumerable<string>? diagnostics = null) =>
        NotApplied(
            CommandResultStatus.Conflict,
            diagnostics);

    public static CommandResult Failed(
        IEnumerable<string>? diagnostics = null) =>
        NotApplied(
            CommandResultStatus.Failed,
            diagnostics);

    private static CommandResult NotApplied(
        CommandResultStatus status,
        IEnumerable<string>? diagnostics)
    {
        if (status == CommandResultStatus.Applied)
        {
            throw new ArgumentOutOfRangeException(
                nameof(status));
        }

        return new CommandResult(
            status,
            null,
            null,
            null,
            [],
            [],
            CopyDiagnostics(
                diagnostics));
    }

    private static IReadOnlyList<EntityReference> Copy(
        IEnumerable<EntityReference>? entities) =>
        entities is null
            ? []
            : Array.AsReadOnly(
                entities.ToArray());

    private static IReadOnlyList<string> CopyDiagnostics(
        IEnumerable<string>? diagnostics) =>
        diagnostics is null
            ? []
            : Array.AsReadOnly(
                diagnostics.ToArray());
}
