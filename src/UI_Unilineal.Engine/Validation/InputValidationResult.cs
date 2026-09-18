namespace UI_Unilineal.Engine.Validation;

public sealed class InputValidationResult
{
    public InputValidationResult(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool HasErrors => Issues.Any(x => x.Severity == ValidationSeverity.Error);
}
