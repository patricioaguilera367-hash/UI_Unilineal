using System.Text.Json;
using UI_Unilineal.Domain.Grounding;

namespace UI_Unilineal.Engine.Composition;

public sealed class GroundingSchemeCatalogLoader
{
    private static readonly JsonSerializerOptions Options =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    public GroundingSchemeCatalog LoadDirectory(
        string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "Profile directory is required.",
                nameof(directory));
        }

        string path =
            Path.Combine(
                directory,
                "grounding-schemes.json");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Grounding scheme catalog was not found.",
                path);
        }

        CatalogDto dto;

        try
        {
            dto =
                JsonSerializer.Deserialize<CatalogDto>(
                    File.ReadAllText(path),
                    Options)
                ?? throw new InvalidDataException(
                    "Grounding scheme catalog is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Grounding scheme catalog contains invalid JSON.",
                exception);
        }

        GroundingSchemeDefinition[] schemes =
            (dto.Schemes ?? [])
                .Select(Convert)
                .ToArray();

        return new GroundingSchemeCatalog(
            Require(
                dto.SelectionMode,
                "grounding.selectionMode"),
            Require(
                dto.DefaultSchemeId,
                "grounding.defaultSchemeId"),
            schemes);
    }

    private static GroundingSchemeDefinition Convert(
        SchemeDto dto) =>
        new(
            Require(dto.Id, "grounding.scheme.id"),
            Require(
                dto.DisplayName,
                "grounding.scheme.displayName"),
            Require(
                dto.SourceDocument,
                "grounding.scheme.sourceDocument"),
            Require(
                dto.SourceSection,
                "grounding.scheme.sourceSection"),
            dto.SourceFigure,
            dto.DefaultSelected,
            dto.SymbolIds ?? [],
            dto.PreviewPresetId,
            Require(
                dto.ConnectionStatus,
                "grounding.scheme.connectionStatus"),
            (dto.ConnectionPoints ?? [])
                .Select(point =>
                    new GroundingConnectionPointDefinition(
                        Require(
                            point.Id,
                            "grounding.connection.id"),
                        Require(
                            point.SymbolId,
                            "grounding.connection.symbolId"),
                        Require(
                            point.TargetRole,
                            "grounding.connection.targetRole")))
                .ToArray());

    private static string Require(
        string? value,
        string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new InvalidDataException(
                $"Required field '{field}' is missing.")
            : value;

    private sealed class CatalogDto
    {
        public string? SelectionMode { get; init; }

        public string? DefaultSchemeId { get; init; }

        public SchemeDto[]? Schemes { get; init; }
    }

    private sealed class SchemeDto
    {
        public string? Id { get; init; }

        public string? DisplayName { get; init; }

        public string? SourceDocument { get; init; }

        public string? SourceSection { get; init; }

        public string? SourceFigure { get; init; }

        public bool DefaultSelected { get; init; }

        public string[]? SymbolIds { get; init; }

        public string? PreviewPresetId { get; init; }

        public string? ConnectionStatus { get; init; }

        public ConnectionPointDto[]? ConnectionPoints { get; init; }
    }

    private sealed class ConnectionPointDto
    {
        public string? Id { get; init; }

        public string? SymbolId { get; init; }

        public string? TargetRole { get; init; }
    }
}
