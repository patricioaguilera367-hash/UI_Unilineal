namespace UI_Unilineal.Engine.Documents;

public sealed record TitleBlock
{
    public TitleBlock(
        string projectName,
        string drawingTitle,
        string sheetLabel,
        string documentCode,
        string revision)
    {
        ProjectName = Required(projectName, nameof(projectName));
        DrawingTitle = Required(drawingTitle, nameof(drawingTitle));
        SheetLabel = Required(sheetLabel, nameof(sheetLabel));
        DocumentCode = Required(documentCode, nameof(documentCode));
        Revision = Required(revision, nameof(revision));
    }

    public string ProjectName { get; }

    public string DrawingTitle { get; }

    public string SheetLabel { get; }

    public string DocumentCode { get; }

    public string Revision { get; }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}
