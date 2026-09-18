using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Validation;

internal sealed class CompletenessValidator
{
    public IEnumerable<ValidationIssue> Validate(SingleLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var issues = new List<ValidationIssue>();

        ValidateBoards(input, issues);
        ValidateSources(input, issues);
        ValidateCircuits(input, issues);
        ValidateGrounding(input, issues);

        return issues;
    }

    private static void ValidateBoards(
        SingleLineInput input,
        ICollection<ValidationIssue> issues)
    {
        HashSet<EntityUid> suppliedBoards = input.SupplyConnections
            .Where(supply => supply.State == OperationalState.Active)
            .Select(supply => supply.DestinationBoardUid)
            .ToHashSet();

        foreach (BoardInput board in input.Boards)
        {
            EntityReference entity = new(board.Uid, EntityKind.Board);

            if (!suppliedBoards.Contains(board.Uid))
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.BoardWithoutSupply,
                    ValidationSeverity.Warning,
                    $"Board '{board.Uid}' has no incoming supply.",
                    entity));
            }

            if (board.NominalVoltageV is null)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.BoardMissingVoltage,
                    ValidationSeverity.Warning,
                    $"Board '{board.Uid}' has no nominal voltage.",
                    entity,
                    nameof(BoardInput.NominalVoltageV)));
            }
        }
    }

    private static void ValidateSources(
        SingleLineInput input,
        ICollection<ValidationIssue> issues)
    {
        foreach (SourceInput source in input.Sources)
        {
            if (source.NominalVoltageV is not null && source.PhaseCount is not null)
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.SourceIncomplete,
                ValidationSeverity.Warning,
                $"Source '{source.Uid}' is missing voltage or phase-count data.",
                new EntityReference(source.Uid, EntityKind.Source)));
        }
    }

    private static void ValidateCircuits(
        SingleLineInput input,
        ICollection<ValidationIssue> issues)
    {
        foreach (CircuitInput circuit in input.Circuits.Where(
                     circuit => circuit.State == OperationalState.Active))
        {
            EntityReference entity = new(circuit.Uid, EntityKind.Circuit);

            if (circuit.Conductor is null)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.CircuitMissingConductor,
                    ValidationSeverity.Warning,
                    $"Active circuit '{circuit.Uid}' has no conductor data.",
                    entity,
                    nameof(CircuitInput.Conductor)));
            }

            bool hasOvercurrentProtection = input.Protections.Any(
                protection =>
                    protection.State == OperationalState.Active &&
                    protection.Protects == entity &&
                    protection.Kind is ProtectionKind.Breaker or ProtectionKind.Fuse or ProtectionKind.Combined);

            if (!hasOvercurrentProtection)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.CircuitMissingProtection,
                    ValidationSeverity.Warning,
                    $"Active circuit '{circuit.Uid}' has no active overcurrent protection.",
                    entity));
            }
        }
    }

    private static void ValidateGrounding(
        SingleLineInput input,
        ICollection<ValidationIssue> issues)
    {
        foreach (GroundingInput grounding in input.Grounding)
        {
            if (grounding.ConductorSectionMm2 is not null ||
                grounding.ResistanceOhm is not null)
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                ValidationCodes.GroundingIncomplete,
                ValidationSeverity.Warning,
                $"Grounding '{grounding.Uid}' has neither conductor section nor resistance.",
                new EntityReference(grounding.Uid, EntityKind.Grounding)));
        }
    }
}
