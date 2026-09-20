using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Playground.Fixtures;

public static class SymbolGallerySceneBuilder
{
    private const double SceneMarginMm = 10;
    private const double CellWidthMm = 72;
    private const double CellHeightMm = 54;
    private const int Columns = 4;
    private const double GridSpacingMm = 20;
    private const double GridClearanceMm = 1.5;

    public static DiagramScene Build(
        RIC18DrawingProfile profile,
        bool showGrid = true,
        bool showBounds = true,
        bool showAnchors = false)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var elements = new List<SceneElement>();
        var nominalBounds = new List<MmRect>();
        var grouped = profile.Symbols
            .OrderBy(symbol => FamilyOrder(Family(symbol)))
            .ThenBy(symbol => Family(symbol), StringComparer.Ordinal)
            .ThenBy(SymbolOrder)
            .ThenBy(symbol => symbol.Id, StringComparer.Ordinal)
            .GroupBy(Family)
            .ToArray();

        int row = 0;
        foreach (IGrouping<string, SymbolDefinition> family in grouped)
        {
            double headingY = SceneMarginMm + row * CellHeightMm;
            elements.Add(Text(
                $"gallery/family/{Slug(family.Key)}",
                new MmRect(SceneMarginMm, headingY, 200, 5),
                family.Key,
                "TECH",
                zIndex: 10));

            row++;

            int index = 0;
            foreach (SymbolDefinition symbol in family)
            {
                int column = index % Columns;
                int familyRow = index / Columns;
                MmRect bounds = AddSymbolCell(
                    elements,
                    symbol,
                    SceneMarginMm + column * CellWidthMm,
                    SceneMarginMm + (row + familyRow) * CellHeightMm,
                    showBounds,
                    showAnchors);
                nominalBounds.Add(bounds);
                index++;
            }

            row += (int)Math.Ceiling(family.Count() / (double)Columns);
        }

        double width = SceneMarginMm * 2 + Columns * CellWidthMm;
        double height = SceneMarginMm * 2 + Math.Max(1, row) * CellHeightMm;

        if (showGrid)
        {
            AddGrid(
                elements,
                width,
                height,
                showBounds ? nominalBounds : []);
        }

        var metadata = new DiagramSceneMetadata(
            profile.ProfileId,
            profile.Version,
            "symbol-gallery",
            "symbol-gallery-v4",
            $"symbol-gallery:grid={showGrid};bounds={showBounds};anchors={showAnchors}",
            "symbol-gallery");

        return new DiagramScene(
            new MmRect(0, 0, width, height),
            elements,
            metadata);
    }

    private static MmRect AddSymbolCell(
        ICollection<SceneElement> elements,
        SymbolDefinition symbol,
        double x,
        double y,
        bool showBounds,
        bool showAnchors)
    {
        const double previewWidth = 62;
        const double previewHeight = 30;

        int? breakerMultiplicity = BreakerMultiplicity(symbol.Id);
        int? rcdMultiplicity = RcdMultiplicity(symbol.Id);
        bool hasRightSideLabel =
            breakerMultiplicity is not null ||
            rcdMultiplicity is not null;

        double symbolX =
            hasRightSideLabel
                ? x + 8
                : x + (previewWidth - symbol.NominalBounds.Width) / 2;
        double symbolY =
            y + 5 + (previewHeight - symbol.NominalBounds.Height) / 2;

        var symbolBounds = new MmRect(
            symbolX,
            symbolY,
            symbol.NominalBounds.Width,
            symbol.NominalBounds.Height);

        string slug = Slug(symbol.Id);

        if (showBounds)
        {
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
                "GALLERY_BOUNDS"));

            double centerX = symbolBounds.X + symbolBounds.Width / 2;
            double centerY = symbolBounds.Y + symbolBounds.Height / 2;

            elements.Add(Line(
                $"gallery/{slug}/guide-x",
                new MmPoint(symbolBounds.X - 1.5, centerY),
                new MmPoint(symbolBounds.Right + 1.5, centerY),
                "GALLERY_GUIDE",
                SceneLayer.Annotation,
                0));

            elements.Add(Line(
                $"gallery/{slug}/guide-y",
                new MmPoint(centerX, symbolBounds.Y - 1.5),
                new MmPoint(centerX, symbolBounds.Bottom + 1.5),
                "GALLERY_GUIDE",
                SceneLayer.Annotation,
                0));
        }

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

        if (showAnchors)
        {
            foreach (AnchorDefinition anchor in symbol.Anchors)
            {
                double anchorX =
                    symbolX +
                    (anchor.Point.X - symbol.NominalBounds.X);
                double anchorY =
                    symbolY +
                    (anchor.Point.Y - symbol.NominalBounds.Y);
                const double radius = 0.8;

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
        }

        if (breakerMultiplicity is int breakerPoles)
        {
            AddBreakerSampleLabel(
                elements,
                slug,
                x + 26,
                symbolY + 3,
                breakerPoles);
        }

        if (rcdMultiplicity is int rcdPoles)
        {
            AddRcdSampleLabel(
                elements,
                slug,
                x + 26,
                symbolY + 2,
                rcdPoles);
        }

        if (symbol.Id == "CIRCUIT_MARKER")
        {
            AddCircuitMarkerSampleNumber(
                elements,
                slug,
                symbolX,
                symbolY);
        }

        elements.Add(Text(
            $"gallery/{slug}/id",
            new MmRect(x, y + 37, previewWidth, 4),
            DisplayId(symbol),
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

        return symbolBounds;
    }

    private static void AddBreakerSampleLabel(
        ICollection<SceneElement> elements,
        string slug,
        double x,
        double y,
        int multiplicity)
    {
        elements.Add(Text(
            $"gallery/{slug}/rating-poles",
            new MmRect(x, y, 28, 4),
            $"{multiplicity}x...A",
            "TECH",
            10));

        elements.Add(Text(
            $"gallery/{slug}/rating-ka",
            new MmRect(x, y + 4.5, 28, 4),
            "...kA",
            "TECH",
            10));

        elements.Add(Text(
            $"gallery/{slug}/rating-reserve",
            new MmRect(x, y + 9, 28, 4),
            ".......",
            "LABEL_SMALL",
            10));
    }

    private static void AddRcdSampleLabel(
        ICollection<SceneElement> elements,
        string slug,
        double x,
        double y,
        int multiplicity)
    {
        elements.Add(Text(
            $"gallery/{slug}/rating-poles",
            new MmRect(x, y, 30, 4),
            $"{multiplicity}x...A",
            "TECH",
            10));

        elements.Add(Text(
            $"gallery/{slug}/rating-ma",
            new MmRect(x, y + 4.5, 30, 4),
            "....mA",
            "TECH",
            10));

        elements.Add(Text(
            $"gallery/{slug}/rating-type",
            new MmRect(x, y + 9, 30, 4),
            "Tipo...",
            "TECH",
            10));
    }

    private static void AddCircuitMarkerSampleNumber(
        ICollection<SceneElement> elements,
        string slug,
        double symbolX,
        double symbolY)
    {
        elements.Add(Text(
            $"gallery/{slug}/number",
            new MmRect(
                symbolX + 3,
                symbolY + 3,
                10,
                10),
            "1",
            "TECH",
            20,
            SceneTextHorizontalAlignment.Center,
            SceneTextVerticalAlignment.Center));
    }

    private static void AddGrid(
        ICollection<SceneElement> elements,
        double width,
        double height,
        IReadOnlyList<MmRect> exclusions)
    {
        int lineIndex = 0;

        for (double x = 0; x <= width; x += GridSpacingMm)
        {
            IReadOnlyList<(double Start, double End)> blocked =
                MergeIntervals(
                    exclusions
                        .Where(bounds =>
                            x >= bounds.X - GridClearanceMm &&
                            x <= bounds.Right + GridClearanceMm)
                        .Select(bounds =>
                            (
                                Math.Max(0, bounds.Y - GridClearanceMm),
                                Math.Min(height, bounds.Bottom + GridClearanceMm)
                            )));

            AddGridSegments(
                elements,
                vertical: true,
                fixedCoordinate: x,
                extent: height,
                blocked,
                ref lineIndex);
        }

        for (double y = 0; y <= height; y += GridSpacingMm)
        {
            IReadOnlyList<(double Start, double End)> blocked =
                MergeIntervals(
                    exclusions
                        .Where(bounds =>
                            y >= bounds.Y - GridClearanceMm &&
                            y <= bounds.Bottom + GridClearanceMm)
                        .Select(bounds =>
                            (
                                Math.Max(0, bounds.X - GridClearanceMm),
                                Math.Min(width, bounds.Right + GridClearanceMm)
                            )));

            AddGridSegments(
                elements,
                vertical: false,
                fixedCoordinate: y,
                extent: width,
                blocked,
                ref lineIndex);
        }
    }

    private static void AddGridSegments(
        ICollection<SceneElement> elements,
        bool vertical,
        double fixedCoordinate,
        double extent,
        IReadOnlyList<(double Start, double End)> blocked,
        ref int lineIndex)
    {
        double cursor = 0;

        foreach ((double start, double end) in blocked)
        {
            if (start - cursor > 0.05)
            {
                AddGridSegment(
                    elements,
                    vertical,
                    fixedCoordinate,
                    cursor,
                    start,
                    lineIndex++);
            }

            cursor = Math.Max(cursor, end);
        }

        if (extent - cursor > 0.05)
        {
            AddGridSegment(
                elements,
                vertical,
                fixedCoordinate,
                cursor,
                extent,
                lineIndex++);
        }
    }

    private static void AddGridSegment(
        ICollection<SceneElement> elements,
        bool vertical,
        double fixedCoordinate,
        double start,
        double end,
        int index)
    {
        MmPoint from =
            vertical
                ? new MmPoint(fixedCoordinate, start)
                : new MmPoint(start, fixedCoordinate);
        MmPoint to =
            vertical
                ? new MmPoint(fixedCoordinate, end)
                : new MmPoint(end, fixedCoordinate);

        elements.Add(Line(
            $"gallery/grid/segment-{index}",
            from,
            to,
            "GALLERY_GRID",
            SceneLayer.Background,
            -100));
    }

    private static IReadOnlyList<(double Start, double End)> MergeIntervals(
        IEnumerable<(double Start, double End)> intervals)
    {
        var ordered = intervals
            .Where(interval => interval.End > interval.Start)
            .OrderBy(interval => interval.Start)
            .ToArray();

        if (ordered.Length == 0)
        {
            return [];
        }

        var merged = new List<(double Start, double End)>();
        double start = ordered[0].Start;
        double end = ordered[0].End;

        for (int index = 1; index < ordered.Length; index++)
        {
            (double nextStart, double nextEnd) = ordered[index];

            if (nextStart <= end)
            {
                end = Math.Max(end, nextEnd);
                continue;
            }

            merged.Add((start, end));
            start = nextStart;
            end = nextEnd;
        }

        merged.Add((start, end));
        return merged;
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
        int zIndex,
        SceneTextHorizontalAlignment horizontalAlignment =
            SceneTextHorizontalAlignment.Start,
        SceneTextVerticalAlignment verticalAlignment =
            SceneTextVerticalAlignment.Top) =>
        new(
            new SceneId(id),
            bounds,
            SceneLayer.Text,
            zIndex,
            SceneVisibility.Interactive,
            null,
            null,
            value,
            styleId,
            horizontalAlignment,
            verticalAlignment);

    private static string Family(SymbolDefinition symbol)
    {
        if (BreakerMultiplicity(symbol.Id) is not null)
        {
            return "2 — BREAKER variants";
        }

        if (RcdMultiplicity(symbol.Id) is not null)
        {
            return "3 — RCD variants";
        }

        if (symbol.Id is "CIRCUIT_MARKER" or "CIRCUIT_USE_TEXT")
        {
            return "5 — Circuit marker";
        }

        return symbol.SemanticRole switch
        {
            "SourceUtility" or "ServiceEntrance" or "ServiceEntranceFrame" or "Meter" or
            "ServiceEntranceText" => "1 — Supply / empalme",
            "Breaker" or "ResidualCurrentDevice" or "Fuse" => "4 — Protection · generic / compatibility",
            "Bus" or "Ground" or "ServiceGround" or "ProtectiveGround" or "ConnectionNode" =>
                "6 — Distribution & grounding",
            "FinalLoad" => "7 — Legacy final-load compatibility",
            _ => "8 — Destinations & fallback"
        };
    }

    private static int FamilyOrder(string family) =>
        family.Length > 0 && char.IsDigit(family[0])
            ? family[0] - '0'
            : 99;

    private static int SymbolOrder(SymbolDefinition symbol) =>
        BreakerMultiplicity(symbol.Id) ??
        RcdMultiplicity(symbol.Id) ??
        int.MaxValue;

    private static int? BreakerMultiplicity(string symbolId) =>
        VariantMultiplicity(
            symbolId,
            "BREAKER_",
            [1, 2, 3, 4]);

    private static int? RcdMultiplicity(string symbolId) =>
        VariantMultiplicity(
            symbolId,
            "RCD_",
            [2, 4]);

    private static int? VariantMultiplicity(
        string symbolId,
        string prefix,
        IReadOnlyCollection<int> allowed)
    {
        if (!symbolId.StartsWith(prefix, StringComparison.Ordinal) ||
            !symbolId.EndsWith("X", StringComparison.Ordinal))
        {
            return null;
        }

        string value = symbolId[prefix.Length..^1];

        return int.TryParse(value, out int multiplicity) &&
               allowed.Contains(multiplicity)
            ? multiplicity
            : null;
    }

    private static string DisplayId(SymbolDefinition symbol) =>
        symbol.Id switch
        {
            "BREAKER" => "BREAKER · generic compatibility",
            "RCD" => "RCD · generic compatibility",
            "GROUND" => "GROUND · legacy compatibility",
            "GROUND_TP" => "GROUND_TP · Tierra de protección",
            "GROUND_TS" => "GROUND_TS · Tierra de servicio",
            "FINAL_LOAD" => "FINAL_LOAD · legacy compatibility",
            _ => symbol.Id
        };

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
