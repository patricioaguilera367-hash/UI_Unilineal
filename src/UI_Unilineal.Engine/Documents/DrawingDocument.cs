using System.Collections.ObjectModel;

namespace UI_Unilineal.Engine.Documents;

public sealed class DrawingDocument
{
    public DrawingDocument(
        string documentId,
        string revision,
        IEnumerable<DrawingSheet> sheets)
    {
        DocumentId = Required(documentId, nameof(documentId));
        Revision = Required(revision, nameof(revision));
        ArgumentNullException.ThrowIfNull(sheets);

        DrawingSheet[] ordered = sheets
            .OrderBy(sheet => sheet.SheetNumber)
            .ToArray();

        if (ordered.Length == 0)
        {
            throw new ArgumentException(
                "A drawing document requires at least one sheet.",
                nameof(sheets));
        }

        for (int index = 0; index < ordered.Length; index++)
        {
            int expected = index + 1;
            if (ordered[index].SheetNumber != expected)
            {
                throw new ArgumentException(
                    "Sheet numbers must be one-based and contiguous.",
                    nameof(sheets));
            }
        }

        Sheets = new ReadOnlyCollection<DrawingSheet>(ordered);
    }

    public string DocumentId { get; }

    public string Revision { get; }

    public IReadOnlyList<DrawingSheet> Sheets { get; }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Required.", parameterName)
            : value;
}
