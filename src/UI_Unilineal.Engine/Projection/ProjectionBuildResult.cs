using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Projection;

public sealed record ProjectionBuildResult(
    SingleLineProjection? Projection,
    InputValidationResult Validation)
{
    public bool Success => Projection is not null && !Validation.HasErrors;
}
