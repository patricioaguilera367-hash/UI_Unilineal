using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Projection;

public static class ProjectionStatusResolver
{
    public static ProjectionStatus Resolve(
        DataState dataState,
        ResultState? resultState,
        IEnumerable<ProjectionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        ProjectionIssue[] materialized = issues.ToArray();

        if (materialized.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            return ProjectionStatus.Error;
        }

        if (materialized.Any(issue => issue.Severity == ValidationSeverity.Warning))
        {
            return ProjectionStatus.Warning;
        }

        if (dataState == DataState.Invalid)
        {
            return ProjectionStatus.Error;
        }

        if (dataState == DataState.Incomplete)
        {
            return ProjectionStatus.Warning;
        }

        return resultState switch
        {
            ResultState.Pending => ProjectionStatus.Pending,
            ResultState.Stale => ProjectionStatus.Stale,
            ResultState.Missing or ResultState.Unknown => ProjectionStatus.Unknown,
            ResultState.Current when dataState == DataState.Complete => ProjectionStatus.Ok,
            null when dataState == DataState.Complete => ProjectionStatus.Ok,
            _ => ProjectionStatus.Unknown
        };
    }
}
