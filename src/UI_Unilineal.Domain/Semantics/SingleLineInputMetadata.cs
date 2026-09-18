namespace UI_Unilineal.Domain.Semantics;

public sealed record SingleLineInputMetadata(
    string SchemaVersion,
    string? SourceSystem,
    string? SourceReference);
