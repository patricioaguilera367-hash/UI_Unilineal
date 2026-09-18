using UI_Unilineal.Domain.Semantics;
using UI_Unilineal.Engine.Projection;
using UI_Unilineal.Engine.Validation;

namespace UI_Unilineal.Engine.Tests.Projection;

public sealed class ProjectionStatusResolverTests
{
    [Fact]
    public void Resolve_ErrorIssue_HasHighestPrecedence()
    {
        ProjectionStatus status = ProjectionStatusResolver.Resolve(
            DataState.Incomplete,
            ResultState.Pending,
            [Issue(ValidationSeverity.Warning), Issue(ValidationSeverity.Error)]);

        Assert.Equal(ProjectionStatus.Error, status);
    }

    [Fact]
    public void Resolve_WarningIssue_OutranksInvalidDataAndResultState()
    {
        ProjectionStatus status = ProjectionStatusResolver.Resolve(
            DataState.Invalid,
            ResultState.Pending,
            [Issue(ValidationSeverity.Warning)]);

        Assert.Equal(ProjectionStatus.Warning, status);
    }

    [Theory]
    [InlineData(DataState.Invalid, ResultState.Current, ProjectionStatus.Error)]
    [InlineData(DataState.Incomplete, ResultState.Current, ProjectionStatus.Warning)]
    [InlineData(DataState.Complete, ResultState.Pending, ProjectionStatus.Pending)]
    [InlineData(DataState.Complete, ResultState.Stale, ProjectionStatus.Stale)]
    [InlineData(DataState.Complete, ResultState.Missing, ProjectionStatus.Unknown)]
    [InlineData(DataState.Complete, ResultState.Unknown, ProjectionStatus.Unknown)]
    [InlineData(DataState.Complete, ResultState.Current, ProjectionStatus.Ok)]
    public void Resolve_UsesDefinedPrecedenceWithoutIssues(
        DataState dataState,
        ResultState resultState,
        ProjectionStatus expected)
    {
        Assert.Equal(
            expected,
            ProjectionStatusResolver.Resolve(dataState, resultState, []));
    }

    [Fact]
    public void Resolve_CompleteDataWithoutResult_IsOk()
    {
        Assert.Equal(
            ProjectionStatus.Ok,
            ProjectionStatusResolver.Resolve(DataState.Complete, null, []));
    }

    private static ProjectionIssue Issue(ValidationSeverity severity) =>
        new("TEST", severity, "test", null, null);
}
