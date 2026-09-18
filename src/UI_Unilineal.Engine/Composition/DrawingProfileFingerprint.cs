using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Engine.Composition;

public static class DrawingProfileFingerprint
{
    public static string Compute(RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var builder = new StringBuilder();

        Value(builder, "PROFILE");
        Value(builder, profile.ProfileId);
        Value(builder, profile.Version);
        Value(builder, profile.SourceDocument);

        foreach (GraphicRuleSource source in profile.Provenance
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            Value(builder, "PROVENANCE");
            Value(builder, source.Id);
            Value(builder, source.Classification.ToString());
            Value(builder, source.Document);
            Value(builder, source.Section);
            Value(builder, source.Annex);
            Value(builder, source.Figure);
            Value(builder, source.Description);
        }

        foreach (LineStyleDefinition style in profile.LineStyles
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            Value(builder, "LINE_STYLE");
            Value(builder, style.Id);
            Value(builder, style.Role.ToString());
            Number(builder, style.WidthMm);
            Value(builder, style.Pattern.ToString());
            Value(builder, style.ProvenanceId);
        }

        foreach (TextStyleDefinition style in profile.TextStyles
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            Value(builder, "TEXT_STYLE");
            Value(builder, style.Id);
            Value(builder, style.FontFamily);
            Number(builder, style.HeightMm);
            Value(builder, style.Bold ? "1" : "0");
            Value(builder, style.ProvenanceId);
        }

        AppendLayout(builder, profile.Layout);

        foreach (SymbolDefinition symbol in profile.Symbols
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            AppendSymbol(builder, symbol);
        }

        foreach (BlockDefinition block in profile.Blocks
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            AppendBlock(builder, block);
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static void AppendLayout(
        StringBuilder builder,
        LayoutProfile layout)
    {
        Value(builder, "LAYOUT");
        Number(builder, layout.GridMm);
        Number(builder, layout.HorizontalGapMm);
        Number(builder, layout.VerticalGapMm);
        Number(builder, layout.BranchGapMm);
        Number(builder, layout.RouteClearanceMm);
        Number(builder, layout.TextPaddingMm);
        Number(builder, layout.MaxBoardDetailWidthMm);
        Number(builder, layout.ContinuationRowGapMm);
        Value(builder, layout.ProvenanceId);
    }

    private static void AppendSymbol(
        StringBuilder builder,
        SymbolDefinition symbol)
    {
        Value(builder, "SYMBOL");
        Value(builder, symbol.Id);
        Value(builder, symbol.SemanticRole);
        Rect(builder, symbol.NominalBounds);

        foreach (var anchor in symbol.Anchors
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            Value(builder, "ANCHOR");
            Value(builder, anchor.Id);
            Value(builder, anchor.Role.ToString());
            Point(builder, anchor.Point);
            Value(builder, anchor.Direction.ToString());
        }

        foreach (SymbolPrimitive primitive in symbol.Primitives)
        {
            AppendPrimitive(builder, primitive);
        }

        foreach (var label in symbol.LabelSlots
                     .OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            Value(builder, "LABEL");
            Value(builder, label.Id);
            Rect(builder, label.Bounds);
            Value(builder, label.Priority.ToString(CultureInfo.InvariantCulture));
            Value(builder, label.Required ? "1" : "0");
            Value(builder, label.TextStyleId);
        }

        foreach (string provenanceId in symbol.ProvenanceIds
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            Value(builder, "SYMBOL_PROVENANCE");
            Value(builder, provenanceId);
        }
    }

    private static void AppendPrimitive(
        StringBuilder builder,
        SymbolPrimitive primitive)
    {
        switch (primitive)
        {
            case LineSymbolPrimitive line:
                Value(builder, "LINE");
                Value(builder, line.LineStyleId);
                Point(builder, line.Start);
                Point(builder, line.End);
                break;

            case PolylineSymbolPrimitive polyline:
                Value(builder, "POLYLINE");
                Value(builder, polyline.LineStyleId);
                Value(
                    builder,
                    polyline.Points.Count.ToString(CultureInfo.InvariantCulture));
                foreach (MmPoint point in polyline.Points)
                {
                    Point(builder, point);
                }
                break;

            case RectangleSymbolPrimitive rectangle:
                Value(builder, "RECTANGLE");
                Value(builder, rectangle.LineStyleId);
                Rect(builder, rectangle.Rectangle);
                break;

            case CircleSymbolPrimitive circle:
                Value(builder, "CIRCLE");
                Value(builder, circle.LineStyleId);
                Point(builder, circle.Center);
                Number(builder, circle.Radius);
                break;

            case ArcSymbolPrimitive arc:
                Value(builder, "ARC");
                Value(builder, arc.LineStyleId);
                Point(builder, arc.Center);
                Number(builder, arc.Radius);
                Number(builder, arc.StartDegrees);
                Number(builder, arc.SweepDegrees);
                break;

            case PathSymbolPrimitive path:
                Value(builder, "PATH");
                Value(builder, path.LineStyleId);
                Value(builder, path.Data);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported symbol primitive '{primitive.GetType().FullName}'.");
        }
    }

    private static void AppendBlock(
        StringBuilder builder,
        BlockDefinition block)
    {
        Value(builder, "BLOCK");
        Value(builder, block.Id);
        Value(builder, block.SemanticRole);
        Number(builder, block.MinimumSize.Width);
        Number(builder, block.MinimumSize.Height);

        foreach (BlockPartDefinition part in block.Parts)
        {
            Value(builder, "PART");
            Value(builder, part.Id);
            Value(builder, part.SymbolId);
            Point(builder, part.Offset);
            Value(builder, part.LabelSlotId);
        }

        foreach (string provenanceId in block.ProvenanceIds
                     .OrderBy(x => x, StringComparer.Ordinal))
        {
            Value(builder, "BLOCK_PROVENANCE");
            Value(builder, provenanceId);
        }
    }

    private static void Point(StringBuilder builder, MmPoint point)
    {
        Number(builder, point.X);
        Number(builder, point.Y);
    }

    private static void Rect(StringBuilder builder, MmRect rect)
    {
        Number(builder, rect.X);
        Number(builder, rect.Y);
        Number(builder, rect.Width);
        Number(builder, rect.Height);
    }

    private static void Number(StringBuilder builder, double value) =>
        Value(builder, value.ToString("R", CultureInfo.InvariantCulture));

    private static void Value(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder
            .Append(value.Length.ToString(CultureInfo.InvariantCulture))
            .Append(':')
            .Append(value);
    }
}
