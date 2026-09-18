using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Validation;

public sealed record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    EntityReference? Entity = null,
    string? Field = null);
