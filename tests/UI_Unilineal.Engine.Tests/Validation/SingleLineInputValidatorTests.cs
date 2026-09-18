using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Tests.Fixtures;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Validation;

public sealed class SingleLineInputValidatorTests
{
    [Fact]
    public void Validate_DuplicateBoardUid_ReturnsError()
    {
        SingleLineInput source = SemanticFixtureFactory.Minimal();
        BoardInput board = source.Boards[0];
        var invalid = new SingleLineInput(
            source.Project,
            source.Sources,
            [board, board],
            source.Buses,
            source.Circuits,
            source.SupplyConnections,
            source.Protections,
            source.Grounding,
            source.Results,
            source.Metadata);

        InputValidationResult result = new SingleLineInputValidator().Validate(invalid);

        Assert.Contains(
            result.Issues,
            x => x.Code == ValidationCodes.DuplicateEntityUid &&
                 x.Severity == ValidationSeverity.Error);
    }
}
