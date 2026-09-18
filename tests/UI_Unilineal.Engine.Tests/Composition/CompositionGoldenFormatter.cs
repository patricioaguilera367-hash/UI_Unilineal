using System.Text;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Tests.Composition;

internal static class CompositionGoldenFormatter
{
    public static string Format(DrawingComposition composition)
    {
        var builder = new StringBuilder();

        Append(
            builder,
            "COMPOSITION",
            composition.Kind,
            composition.Scope?.Kind.ToString() ?? "-",
            composition.Scope?.Uid.ToString() ?? "-",
            composition.InputFingerprint,
            composition.ProfileFingerprint);

        foreach (CompositionBlock block in composition.Blocks)
        {
            Append(
                builder,
                "BLOCK",
                block.Id,
                block.BlockDefinitionId,
                block.SemanticRole,
                block.Entity?.Kind.ToString() ?? "-",
                block.Entity?.Uid.ToString() ?? "-",
                block.Status,
                block.ParentId ?? "-",
                Map(block.Labels),
                Map(block.SymbolOverrides));
        }

        foreach (CompositionConnection connection in composition.Connections)
        {
            Append(
                builder,
                "CONNECTION",
                connection.Id,
                Anchor(connection.Source),
                Anchor(connection.Target),
                connection.LineStyleId,
                connection.SemanticEntity?.Kind.ToString() ?? "-",
                connection.SemanticEntity?.Uid.ToString() ?? "-");
        }

        return builder.ToString();
    }

    private static string Anchor(CompositionAnchorRef anchor) =>
        $"{anchor.BlockId}@{anchor.Role}:{anchor.PreferredAnchorId ?? "-"}";

    private static string Map(IReadOnlyDictionary<string, string> values) =>
        values.Count == 0
            ? "-"
            : string.Join(
                ",",
                values
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => $"{pair.Key}={pair.Value}"));

    private static void Append(StringBuilder builder, params object[] values)
    {
        builder.AppendJoin('|', values);
        builder.Append('\n');
    }
}
