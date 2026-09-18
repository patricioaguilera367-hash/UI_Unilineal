using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Validation;

public sealed class SingleLineInputValidator
{
    public InputValidationResult Validate(SingleLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var index = new SemanticEntityIndex(input);
        var issues = new List<ValidationIssue>();

        foreach (EntityReference duplicate in index.Duplicates)
        {
            issues.Add(new ValidationIssue(
                ValidationCodes.DuplicateEntityUid,
                ValidationSeverity.Error,
                $"Duplicate semantic identity '{duplicate.Kind}:{duplicate.Uid}'.",
                duplicate));
        }

        return new InputValidationResult(issues);
    }
}
