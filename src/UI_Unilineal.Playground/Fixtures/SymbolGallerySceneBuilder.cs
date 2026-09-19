using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Playground.Fixtures;

public static class SymbolGallerySceneBuilder
{
    private const double SceneMarginMm = 10;
    private const double CellWidthMm = 58;
    private const double CellHeightMm = 54;
    private const int Columns = 4;

    public static DiagramScene Build(RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var elements = new List<SceneElement>();
        var grouped = profile.Symbols
            .OrderBy(symbol => FamilyOrder(Family(symbol)))
            .ThenBy(symbol => symbol.Id, StringComparer.Ordinal)
            .GroupBy(Family)
            .ToArray();

        int row = 0;
        foreach (IGrouping<string, SymbolDefinition> family in grouped)
        {
            double headingY = SceneMarginMm + row * CellHeightMm;
            elements.Add(Text(
                $"gallery/family/{Slug(family.Key)}",
                new MmRect(SceneMarginMm, headingY, 120, 5),
                family.Key,
                "TECH",
                zIndex: 10));

            row++;

            int index = 0;
            foreach (SymbolDefinition symbol in family)
            {
                int column = index % Columns;
                int familyRow = index / Columns;
                AddSymbolCell(
                    elements,
                    symbol,
                    SceneMarginMm + column * CellWidthMm,
                    SceneMarginMm + (row + familyRow) * CellHeightMm);
                index++;
            }

            row += (int)Math.Ceiling(family.Count() / (double)Columns);
        }

        double width = SceneMarginMm * 2 + Columns * CellWidthMm;
        double height = SceneMarginMm * 2 + Math.Max(1, row) * CellHeightMm;

        AddGrid(elements, width, height);

        var metadata = new DiagramSceneMetadata(
            profile.ProfileId,
            profile.Version,
            "symbol-gallery",
            "symbol-gallery-v1",
            "symbol-gallery",
            "symbol-gallery");

        return new DiagramScene(
            new MmRect(0, 0, width, height),
            elements,
            metadata);
    }

    private static void AddSymbolCell(
        ICollection<SceneElement> elements,
        SymbolDefinition symbol,
        double x,
        double y)
    {
        const double previewWidth = 48;
        const double previewHeight = 30;

        double symbolX =
            x + (previewWidth - symbol.NominalBounds.Width) / 2;
        double symbolY =
            y + 5 + (previewHeight - symbol.NominalBounds.Height) / 2;

        var symbolBounds = new MmRect(
            symbolX,
            symbolY,
            symbol.NominalBounds.Width,
            symbol.NominalBounds.Height);

        string slug = Slug(symbol.Id);

        elements.Add(new RectangleSceneElement(
            new SceneId($"gallery/{slug}/bounds"),
            symbolBounds,
            SceneLayer.Annotation,
            1,
            SceneVisibility.Interactive,
            null,
            new Dictionary<string, string>
            {
                ["gallery"] = "nominal-bounds"
            },
            "REFERENCE"));

        double centerX = symbolBounds.X + symbolBounds.Width / 2;
        double centerY = symbolBounds.Y + symbolBounds.Height / 2;

        elements.Add(Line(
            $"gallery/{slug}/guide-x",
            new MmPoint(symbolBounds.X - 2, centerY),
            new MmPoint(symbolBounds.Right + 2, centerY),
            "REFERENCE",
            SceneLayer.Annotation,
            0));

        elements.Add(Line(
            $"gallery/{slug}/guide-y",
            new MmPoint(centerX, symbolBounds.Y - 2),
            new MmPoint(centerX, symbolBounds.Bottom + 2),
            "REFERENCE",
            SceneLayer.Annotation,
            0));

        elements.Add(new SymbolSceneElement(
            new SceneId($"gallery/{slug}/symbol"),
            symbolBounds,
            SceneLayer.Symbol,
            5,
            SceneVisibility.Both,
            null,
            new Dictionary<string, string>
            {
                ["symbolId"] = symbol.Id,
                ["semanticRole"] = symbol.SemanticRole
            },
            symbol.Id));

        foreach (AnchorDefinition anchor in symbol.Anchors)
        {
            double anchorX =
                symbolX +
                (anchor.Point.X - symbol.NominalBounds.X);
            double anchorY =
                symbolY +
                (anchor.Point.Y - symbol.NominalBounds.Y);
            const double radius = 0.9;

            elements.Add(new CircleSceneElement(
                new SceneId($"gallery/{slug}/anchor-{Slug(anchor.Id)}"),
                new MmRect(
                    anchorX - radius,
                    anchorY - radius,
                    radius * 2,
                    radius * 2),
                SceneLayer.Interaction,
                20,
                SceneVisibility.Interactive,
                null,
                new Dictionary<string, string>
                {
                    ["anchorId"] = anchor.Id,
                    ["anchorRole"] = anchor.Role.ToString()
                },
                new MmPoint(anchorX, anchorY),
                radius,
                "ANNOTATION"));
        }

        elements.Add(Text(
            $"gallery/{slug}/id",
            new MmRect(x, y + 37, previewWidth, 4),
            symbol.Id,
            "TECH",
            10));

        elements.Add(Text(
            $"gallery/{slug}/role",
            new MmRect(x, y + 42, previewWidth, 4),
            symbol.SemanticRole,
            "LABEL_SMALL",
            10));

        elements.Add(Text(
            $"gallery/{slug}/size",
            new MmRect(x, y + 47, previewWidth, 4),
            $"{symbol.NominalBounds.Width:0.##} x {symbol.NominalBounds.Height:0.##} mm",
            "LABEL_SMALL",
            10));
    }

    private static void AddGrid(
        ICollection<SceneElement> elements,
        double width,
        double height)
    {
        const double spacing = 20;

        for (double x = 0; x <= width; x += spacing)
        {
            elements.Add(Line(
                $"gallery/grid/v-{x:0}",
                new MmPoint(x, 0),
                new MmPoint(x, height),
                "GALLERY_GRID",
                SceneLayer.Background,
                -100));
        }

        for (double y = 0; y <= height; y += spacing)
        {
            elements.Add(Line(
                $"gallery/grid/h-{y:0}",
                new MmPoint(0, y),
                new MmPoint(width, y),
                "GALLERY_GRID",
                SceneLayer.Background,
                -100));
        }
    }

    private static LineSceneElement Line(
        string id,
        MmPoint start,
        MmPoint end,
        string styleId,
        SceneLayer layer,
        int zIndex)
    {
        double minX = Math.Min(start.X, end.X);
        double minY = Math.Min(start.Y, end.Y);
        double width = Math.Max(0.01, Math.Abs(end.X - start.X));
        double height = Math.Max(0.01, Math.Abs(end.Y - start.Y));

        return new LineSceneElement(
            new SceneId(id),
            new MmRect(minX, minY, width, height),
            layer,
            zIndex,
            SceneVisibility.Interactive,
            null,
            null,
            start,
            end,
            styleId);
    }

    private static TextSceneElement Text(
        string id,
        MmRect bounds,
        string value,
        string styleId,
        int zIndex) =>
        new(
            new SceneId(id),
            bounds,
            SceneLayer.Text,
            zIndex,
            SceneVisibility.Interactive,
            null,
            null,
            value,
            styleId);

    private static string Family(SymbolDefinition symbol) =>
        symbol.SemanticRole switch
        {
            "SourceUtility" or "ServiceEntrance" => "1 — Supply",
            "Breaker" or "ResidualCurrentDevice" or "Fuse" => "2 — Protection",
            "Bus" or "Ground" or "ConnectionNode" => "3 — Distribution & grounding",
            _ => "4 — Destinations & fallback"
        };

    private static int FamilyOrder(string family) =>
        family.Length > 0 && char.IsDigit(family[0])
            ? family[0] - '0'
            : 99;

    private static string Slug(string value)
    {
        char[] chars = value
            .ToLowerInvariant()
            .Select(character =>
                char.IsLetterOrDigit(character)
                    ? character
                    : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }
}
