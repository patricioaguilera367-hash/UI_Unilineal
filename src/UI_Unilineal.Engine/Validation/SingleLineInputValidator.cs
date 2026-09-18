using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Validation;

public sealed class SingleLineInputValidator
{
    public InputValidationResult Validate(SingleLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var index = new SemanticEntityIndex(input);
        var issues = new List<ValidationIssue>();

        AddDuplicateIssues(index, issues);
        AddReferenceIssues(input, index, issues);
        issues.AddRange(new SupplyTopologyValidator().Validate(input, index));

        return new InputValidationResult(issues);
    }

    private static void AddDuplicateIssues(
        SemanticEntityIndex index,
        ICollection<ValidationIssue> issues)
    {
        foreach (EntityReference duplicate in index.Duplicates)
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.DuplicateEntityUid,
                ValidationSeverity.Error,
                $"Duplicate semantic identity '{duplicate.Kind}:{duplicate.Uid}'.",
                duplicate));
        }
    }

    private static void AddReferenceIssues(
        SingleLineInput input,
        SemanticEntityIndex index,
        ICollection<ValidationIssue> issues)
    {
        foreach (CircuitInput circuit in input.Circuits)
        {
            AddMissingReference(
                index,
                new EntityReference(circuit.BoardUid, EntityKind.Board),
                new EntityReference(circuit.Uid, EntityKind.Circuit),
                nameof(CircuitInput.BoardUid),
                issues);
        }

        foreach (BusInput bus in input.Buses)
        {
            AddMissingReference(
                index,
                new EntityReference(bus.BoardUid, EntityKind.Board),
                new EntityReference(bus.Uid, EntityKind.Bus),
                nameof(BusInput.BoardUid),
                issues);
        }

        foreach (SupplyConnection supply in input.SupplyConnections)
        {
            EntityReference supplyEntity = new(supply.Uid, EntityKind.SupplyConnection);

            AddMissingReference(
                index,
                new EntityReference(supply.DestinationBoardUid, EntityKind.Board),
                supplyEntity,
                nameof(SupplyConnection.DestinationBoardUid),
                issues);

            if (supply.Origin.Kind is not EntityKind.Source and not EntityKind.Board)
            {
                issues.Add(new ValidationIssue(
                    ValidationCodes.InvalidSupplyOrigin,
                    ValidationSeverity.Error,
                    $"Supply origin kind '{supply.Origin.Kind}' is not valid.",
                    supplyEntity,
                    nameof(SupplyConnection.Origin)));
            }
            else
            {
                AddMissingReference(
                    index,
                    supply.Origin,
                    supplyEntity,
                    nameof(SupplyConnection.Origin),
                    issues);
            }

            if (supply.ThroughCircuitUid is not null)
            {
                AddMissingReference(
                    index,
                    new EntityReference(supply.ThroughCircuitUid, EntityKind.Circuit),
                    supplyEntity,
                    nameof(SupplyConnection.ThroughCircuitUid),
                    issues);
            }
        }

        foreach (ProtectionInput protection in input.Protections)
        {
            EntityReference protectionEntity = new(protection.Uid, EntityKind.Protection);
            AddMissingReference(
                index,
                protection.Owner,
                protectionEntity,
                nameof(ProtectionInput.Owner),
                issues);
            AddMissingReference(
                index,
                protection.Protects,
                protectionEntity,
                nameof(ProtectionInput.Protects),
                issues);
        }

        foreach (GroundingInput grounding in input.Grounding)
        {
            AddMissingReference(
                index,
                grounding.Owner,
                new EntityReference(grounding.Uid, EntityKind.Grounding),
                nameof(GroundingInput.Owner),
                issues);
        }

        foreach (ElectricalResultInput result in input.Results)
        {
            AddMissingReference(
                index,
                result.Entity,
                result.Entity,
                nameof(ElectricalResultInput.Entity),
                issues);
        }
    }

    private static void AddMissingReference(
        SemanticEntityIndex index,
        EntityReference target,
        EntityReference owner,
        string field,
        ICollection<ValidationIssue> issues)
    {
        if (index.Exists(target))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            ValidationCodes.MissingReference,
            ValidationSeverity.Error,
            $"Reference '{target.Kind}:{target.Uid}' does not exist.",
            owner,
            field));
    }
}
