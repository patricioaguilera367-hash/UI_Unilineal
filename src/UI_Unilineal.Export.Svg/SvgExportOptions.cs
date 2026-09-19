using UI_Unilineal.Engine.Documents;

namespace UI_Unilineal.Export.Svg;

public sealed class SvgExportOptions
{
    public SvgExportOptions(
        string fileNamePrefix = "drawing",
        ExportPreflightOptions? preflight = null)
    {
        if (string.IsNullOrWhiteSpace(fileNamePrefix))
        {
            throw new ArgumentException(
                "File name prefix is required.",
                nameof(fileNamePrefix));
        }

        FileNamePrefix = fileNamePrefix;
        Preflight = preflight ?? ExportPreflightOptions.NonStrict;
    }

    public string FileNamePrefix { get; }

    public ExportPreflightOptions Preflight { get; }
}

public sealed record SvgExportEntry(
    int SheetNumber,
    string FileName);
