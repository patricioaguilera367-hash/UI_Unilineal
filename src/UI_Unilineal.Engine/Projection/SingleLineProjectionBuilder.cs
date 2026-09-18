using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Projection;

public sealed class SingleLineProjectionBuilder
{
    public ProjectionBuildResult Build(SingleLineInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        InputValidationResult validation = new SingleLineInputValidator().Validate(input);

        if (validation.HasErrors)
        {
            return new ProjectionBuildResult(null, validation);
        }

        ProjectionIssue[] issues = validation.Issues
            .Select(issue => new ProjectionIssue(
                issue.Code,
                issue.Severity,
                issue.Message,
                issue.Entity,
                issue.Field))
            .ToArray();

        var index = new SemanticEntityIndex(input);
        string fingerprint = SingleLineInputFingerprint.Compute(input);

        SummaryProjection summary = new SummaryProjectionBuilder()
            .Build(input, index, issues);

        BoardDetailProjection[] details = input.Boards
            .OrderBy(board => board.Number)
            .ThenBy(board => board.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(board => board.Uid.Value, StringComparer.Ordinal)
            .Select(board => new BoardDetailProjectionBuilder()
                .Build(board, input, index, issues))
            .ToArray();

        var projection = new SingleLineProjection(
            input.Project.Uid,
            summary,
            details,
            issues,
            fingerprint);

        return new ProjectionBuildResult(projection, validation);
    }
}
