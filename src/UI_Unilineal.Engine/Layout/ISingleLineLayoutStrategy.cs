using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class PositionedLayout
{
    private readonly IReadOnlyDictionary<string, PositionedCompositionBlock> _byId;

    public PositionedLayout(
        IEnumerable<PositionedCompositionBlock> blocks,
        MmRect bounds)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        PositionedCompositionBlock[] materialized = blocks
            .OrderBy(block => block.BlockId, StringComparer.Ordinal)
            .ToArray();

        string? duplicate = materialized
            .GroupBy(block => block.BlockId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Positioned block ID '{duplicate}' is duplicated.",
                nameof(blocks));
        }

        Blocks = Array.AsReadOnly(materialized);
        _byId = materialized.ToDictionary(
            block => block.BlockId,
            StringComparer.Ordinal);
        Bounds = bounds;
    }

    public IReadOnlyList<PositionedCompositionBlock> Blocks { get; }

    public MmRect Bounds { get; }

    public PositionedCompositionBlock GetBlock(string blockId)
    {
        if (!_byId.TryGetValue(
                blockId,
                out PositionedCompositionBlock? block))
        {
            throw new KeyNotFoundException(
                $"Positioned block '{blockId}' does not exist.");
        }

        return block;
    }
}

public interface ISingleLineLayoutStrategy
{
    PositionedLayout Layout(
        DrawingComposition composition,
        CompositionMeasurement measurement,
        LayoutProfile profile);
}
