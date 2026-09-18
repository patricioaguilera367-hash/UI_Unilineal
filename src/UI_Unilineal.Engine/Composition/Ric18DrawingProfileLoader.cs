using System.Text.Json;
using System.Text.Json.Serialization;
using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;

namespace UI_Unilineal.Engine.Composition;

public sealed class Ric18DrawingProfileLoader
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public RIC18DrawingProfile LoadDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Profile directory is required.", nameof(directory));
        }

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(directory);
        }

        ProfileHeaderDto header = ReadRequired<ProfileHeaderDto>(directory, "profile.json");
        ProvenanceDto[] provenanceDtos = ReadRequired<ProvenanceDto[]>(directory, "provenance.json");
        LineStyleDto[] lineStyleDtos = ReadRequired<LineStyleDto[]>(directory, "line-styles.json");
        TextStyleDto[] textStyleDtos = ReadRequired<TextStyleDto[]>(directory, "text-styles.json");
        LayoutDto layoutDto = ReadRequired<LayoutDto>(directory, "layout.json");
        SymbolDto[] symbolDtos = ReadRequired<SymbolDto[]>(directory, "symbols.json");
        BlockDto[] blockDtos = ReadRequired<BlockDto[]>(directory, "blocks.json");

        GraphicRuleSource[] provenance = provenanceDtos
            .Select(ConvertProvenance)
            .ToArray();
        LineStyleDefinition[] lineStyles = lineStyleDtos
            .Select(ConvertLineStyle)
            .ToArray();
        TextStyleDefinition[] textStyles = textStyleDtos
            .Select(ConvertTextStyle)
            .ToArray();
        SymbolDefinition[] symbols = symbolDtos
            .Select(ConvertSymbol)
            .ToArray();
        BlockDefinition[] blocks = blockDtos
            .Select(ConvertBlock)
            .ToArray();

        return new RIC18DrawingProfile(
            Require(header.ProfileId, "profile.json:profileId"),
            Require(header.Version, "profile.json:version"),
            header.SourceDocument,
            provenance,
            symbols,
            blocks,
            lineStyles,
            textStyles,
            ConvertLayout(layoutDto));
    }

    private static GraphicRuleSource ConvertProvenance(ProvenanceDto dto) =>
        new(
            Require(dto.Id, "provenance.id"),
            dto.Classification ??
                throw Invalid("provenance.classification"),
            dto.Document,
            dto.Section,
            dto.Annex,
            dto.Figure,
            Require(dto.Description, "provenance.description"));

    private static LineStyleDefinition ConvertLineStyle(LineStyleDto dto) =>
        new(
            Require(dto.Id, "lineStyle.id"),
            dto.Role ?? throw Invalid("lineStyle.role"),
            dto.WidthMm ?? throw Invalid("lineStyle.widthMm"),
            dto.Pattern ?? throw Invalid("lineStyle.pattern"),
            Require(dto.ProvenanceId, "lineStyle.provenanceId"));

    private static TextStyleDefinition ConvertTextStyle(TextStyleDto dto) =>
        new(
            Require(dto.Id, "textStyle.id"),
            Require(dto.FontFamily, "textStyle.fontFamily"),
            dto.HeightMm ?? throw Invalid("textStyle.heightMm"),
            dto.Bold ?? throw Invalid("textStyle.bold"),
            Require(dto.ProvenanceId, "textStyle.provenanceId"));

    private static LayoutProfile ConvertLayout(LayoutDto dto) =>
        new(
            dto.GridMm ?? throw Invalid("layout.gridMm"),
            dto.HorizontalGapMm ?? throw Invalid("layout.horizontalGapMm"),
            dto.VerticalGapMm ?? throw Invalid("layout.verticalGapMm"),
            dto.BranchGapMm ?? throw Invalid("layout.branchGapMm"),
            dto.RouteClearanceMm ?? throw Invalid("layout.routeClearanceMm"),
            dto.TextPaddingMm ?? throw Invalid("layout.textPaddingMm"),
            dto.MaxBoardDetailWidthMm ?? throw Invalid("layout.maxBoardDetailWidthMm"),
            dto.ContinuationRowGapMm ?? throw Invalid("layout.continuationRowGapMm"),
            Require(dto.ProvenanceId, "layout.provenanceId"));

    private static SymbolDefinition ConvertSymbol(SymbolDto dto)
    {
        AnchorDefinition[] anchors = (dto.Anchors ?? [])
            .Select(anchor => new AnchorDefinition(
                Require(anchor.Id, "symbol.anchor.id"),
                anchor.Role ?? throw Invalid("symbol.anchor.role"),
                Point(anchor.Point, "symbol.anchor.point"),
                anchor.Direction ?? throw Invalid("symbol.anchor.direction")))
            .ToArray();

        SymbolPrimitive[] primitives = (dto.Primitives ?? [])
            .Select(ConvertPrimitive)
            .ToArray();

        LabelSlot[] labels = (dto.LabelSlots ?? [])
            .Select(label => new LabelSlot(
                Require(label.Id, "symbol.label.id"),
                Rect(label.Bounds, "symbol.label.bounds"),
                label.Priority ?? throw Invalid("symbol.label.priority"),
                label.Required ?? throw Invalid("symbol.label.required"),
                Require(label.TextStyleId, "symbol.label.textStyleId")))
            .ToArray();

        return new SymbolDefinition(
            Require(dto.Id, "symbol.id"),
            Require(dto.SemanticRole, "symbol.semanticRole"),
            Rect(dto.NominalBounds, "symbol.nominalBounds"),
            anchors,
            primitives,
            labels,
            dto.ProvenanceIds ?? []);
    }

    private static SymbolPrimitive ConvertPrimitive(PrimitiveDto dto)
    {
        string kind = Require(dto.Kind, "symbol.primitive.kind");
        string styleId = Require(dto.StyleId, "symbol.primitive.styleId");

        return kind.ToLowerInvariant() switch
        {
            "line" => new LineSymbolPrimitive(
                Point(dto.Start, "primitive.start"),
                Point(dto.End, "primitive.end"),
                styleId),
            "polyline" => new PolylineSymbolPrimitive(
                (dto.Points ?? [])
                    .Select(point => Point(point, "primitive.points"))
                    .ToArray(),
                styleId),
            "rectangle" => new RectangleSymbolPrimitive(
                Rect(dto.Rectangle, "primitive.rectangle"),
                styleId),
            "circle" => new CircleSymbolPrimitive(
                Point(dto.Center, "primitive.center"),
                dto.Radius ?? throw Invalid("primitive.radius"),
                styleId),
            "arc" => new ArcSymbolPrimitive(
                Point(dto.Center, "primitive.center"),
                dto.Radius ?? throw Invalid("primitive.radius"),
                dto.StartDegrees ?? throw Invalid("primitive.startDegrees"),
                dto.SweepDegrees ?? throw Invalid("primitive.sweepDegrees"),
                styleId),
            "path" => new PathSymbolPrimitive(
                Require(dto.Data, "primitive.data"),
                styleId),
            _ => throw new InvalidDataException(
                $"Unknown symbol primitive kind '{kind}'.")
        };
    }

    private static BlockDefinition ConvertBlock(BlockDto dto) =>
        new(
            Require(dto.Id, "block.id"),
            Require(dto.SemanticRole, "block.semanticRole"),
            Size(dto.MinimumSize, "block.minimumSize"),
            (dto.Parts ?? [])
                .Select(part => new BlockPartDefinition(
                    Require(part.Id, "block.part.id"),
                    Require(part.SymbolId, "block.part.symbolId"),
                    Point(part.Offset, "block.part.offset"),
                    part.LabelSlotId))
                .ToArray(),
            dto.ProvenanceIds ?? []);

    private static MmPoint Point(PointDto? dto, string field)
    {
        if (dto?.X is null || dto.Y is null)
        {
            throw Invalid(field);
        }

        return new MmPoint(dto.X.Value, dto.Y.Value);
    }

    private static MmSize Size(SizeDto? dto, string field)
    {
        if (dto?.Width is null || dto.Height is null)
        {
            throw Invalid(field);
        }

        return new MmSize(dto.Width.Value, dto.Height.Value);
    }

    private static MmRect Rect(RectDto? dto, string field)
    {
        if (dto?.X is null || dto.Y is null ||
            dto.Width is null || dto.Height is null)
        {
            throw Invalid(field);
        }

        return new MmRect(
            dto.X.Value,
            dto.Y.Value,
            dto.Width.Value,
            dto.Height.Value);
    }

    private static T ReadRequired<T>(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Required drawing profile file '{fileName}' was not found.",
                path);
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, Options) ??
                throw new InvalidDataException(
                    $"Drawing profile file '{fileName}' is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Drawing profile file '{fileName}' contains invalid JSON.",
                exception);
        }
    }

    private static string Require(string? value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Invalid(field)
            : value;

    private static InvalidDataException Invalid(string field) =>
        new($"Required drawing profile field '{field}' is missing or invalid.");

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class ProfileHeaderDto
    {
        public string? ProfileId { get; init; }
        public string? Version { get; init; }
        public string? SourceDocument { get; init; }
    }

    private sealed class ProvenanceDto
    {
        public string? Id { get; init; }
        public GraphicRuleClassification? Classification { get; init; }
        public string? Document { get; init; }
        public string? Section { get; init; }
        public string? Annex { get; init; }
        public string? Figure { get; init; }
        public string? Description { get; init; }
    }

    private sealed class LineStyleDto
    {
        public string? Id { get; init; }
        public LineSemanticRole? Role { get; init; }
        public double? WidthMm { get; init; }
        public LinePattern? Pattern { get; init; }
        public string? ProvenanceId { get; init; }
    }

    private sealed class TextStyleDto
    {
        public string? Id { get; init; }
        public string? FontFamily { get; init; }
        public double? HeightMm { get; init; }
        public bool? Bold { get; init; }
        public string? ProvenanceId { get; init; }
    }

    private sealed class LayoutDto
    {
        public double? GridMm { get; init; }
        public double? HorizontalGapMm { get; init; }
        public double? VerticalGapMm { get; init; }
        public double? BranchGapMm { get; init; }
        public double? RouteClearanceMm { get; init; }
        public double? TextPaddingMm { get; init; }
        public double? MaxBoardDetailWidthMm { get; init; }
        public double? ContinuationRowGapMm { get; init; }
        public string? ProvenanceId { get; init; }
    }

    private sealed class SymbolDto
    {
        public string? Id { get; init; }
        public string? SemanticRole { get; init; }
        public RectDto? NominalBounds { get; init; }
        public AnchorDto[]? Anchors { get; init; }
        public PrimitiveDto[]? Primitives { get; init; }
        public LabelSlotDto[]? LabelSlots { get; init; }
        public string[]? ProvenanceIds { get; init; }
    }

    private sealed class AnchorDto
    {
        public string? Id { get; init; }
        public AnchorRole? Role { get; init; }
        public PointDto? Point { get; init; }
        public AnchorDirection? Direction { get; init; }
    }

    private sealed class PrimitiveDto
    {
        public string? Kind { get; init; }
        public string? StyleId { get; init; }
        public PointDto? Start { get; init; }
        public PointDto? End { get; init; }
        public PointDto[]? Points { get; init; }
        public RectDto? Rectangle { get; init; }
        public PointDto? Center { get; init; }
        public double? Radius { get; init; }
        public double? StartDegrees { get; init; }
        public double? SweepDegrees { get; init; }
        public string? Data { get; init; }
    }

    private sealed class LabelSlotDto
    {
        public string? Id { get; init; }
        public RectDto? Bounds { get; init; }
        public int? Priority { get; init; }
        public bool? Required { get; init; }
        public string? TextStyleId { get; init; }
    }

    private sealed class BlockDto
    {
        public string? Id { get; init; }
        public string? SemanticRole { get; init; }
        public SizeDto? MinimumSize { get; init; }
        public BlockPartDto[]? Parts { get; init; }
        public string[]? ProvenanceIds { get; init; }
    }

    private sealed class BlockPartDto
    {
        public string? Id { get; init; }
        public string? SymbolId { get; init; }
        public PointDto? Offset { get; init; }
        public string? LabelSlotId { get; init; }
    }

    private sealed class PointDto
    {
        public double? X { get; init; }
        public double? Y { get; init; }
    }

    private sealed class SizeDto
    {
        public double? Width { get; init; }
        public double? Height { get; init; }
    }

    private sealed class RectDto
    {
        public double? X { get; init; }
        public double? Y { get; init; }
        public double? Width { get; init; }
        public double? Height { get; init; }
    }
}
