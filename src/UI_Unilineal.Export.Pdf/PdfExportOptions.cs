using UI_Unilineal.Engine.Documents;

namespace UI_Unilineal.Export.Pdf;

public sealed class PdfExportOptions
{
    public PdfExportOptions(ExportPreflightOptions? preflight = null)
    {
        Preflight = preflight ?? ExportPreflightOptions.NonStrict;
    }

    public ExportPreflightOptions Preflight { get; }
}
