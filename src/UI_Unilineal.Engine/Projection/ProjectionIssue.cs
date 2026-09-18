using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Projection;

public sealed record ProjectionIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    EntityReference? Entity,
    string? Field);
