using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Projection;

public sealed record SingleLineProjection(
    EntityUid ProjectUid,
    SummaryProjection Summary,
    IReadOnlyList<BoardDetailProjection> BoardDetails,
    IReadOnlyList<ProjectionIssue> Issues,
    string InputFingerprint);
