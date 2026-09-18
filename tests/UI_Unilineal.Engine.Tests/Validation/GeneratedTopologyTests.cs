using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Validation;

public sealed class GeneratedTopologyTests
{
    [Fact]
    public void GeneratedAcyclicChains_AreStructurallyValid()
    {
        var validator = new SingleLineInputValidator();

        for (int count = 1; count <= 50; count++)
        {
            InputValidationResult result = validator.Validate(SemanticFixtureFactory.BoardChain(count));

            Assert.False(
                result.HasErrors,
                $"Unexpected error for board count {count}: {string.Join("; ", result.Issues)}");
        }
    }

    [Fact]
    public void GeneratedChains_WithBackEdge_ReportSupplyCycle()
    {
        var validator = new SingleLineInputValidator();

        for (int count = 2; count <= 50; count++)
        {
            SingleLineInput source = SemanticFixtureFactory.BoardChain(count);
            BoardInput first = source.Boards[0];
            BoardInput last = source.Boards[^1];
            CircuitInput lastCircuit = source.Circuits.Single(x => x.BoardUid == last.Uid);
            var cycle = new SupplyConnection(
                new EntityUid($"SC-CYCLE-{count:D3}"),
                new EntityReference(last.Uid, EntityKind.Board),
                lastCircuit.Uid,
                first.Uid,
                SupplyRole.Normal,
                0,
                true,
                OperationalState.Active,
                DataState.Complete);

            var invalid = new SingleLineInput(
                source.Project,
                source.Sources,
                source.Boards,
                source.Buses,
                source.Circuits,
                [.. source.SupplyConnections, cycle],
                source.Protections,
                source.Grounding,
                source.Results,
                source.Metadata);

            InputValidationResult result = validator.Validate(invalid);

            Assert.Contains(
                result.Issues,
                issue => issue.Code == ValidationCodes.SupplyCycle &&
                         issue.Severity == ValidationSeverity.Error);
        }
    }
}
