using System.Collections.ObjectModel;
using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Symbols;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class MeasuredBlock
{
    public MeasuredBlock(
        string blockId,
        MmSize size,
        IReadOnlyDictionary<string, TextMeasurement> labels,
        int partCount)
    {
        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException("Block ID is required.", nameof(blockId));
        }

        if (partCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(partCount));
        }

        ArgumentNullException.ThrowIfNull(labels);

        BlockId = blockId;
        Size = size;
        Labels = new ReadOnlyDictionary<string, TextMeasurement>(
            new Dictionary<string, TextMeasurement>(
                labels,
                StringComparer.Ordinal));
        PartCount = partCount;
    }

    public string BlockId { get; }

    public MmSize Size { get; }

    public IReadOnlyDictionary<string, TextMeasurement> Labels { get; }

    public int PartCount { get; }
}

public sealed class CompositionMeasurement
{
    public CompositionMeasurement(IEnumerable<MeasuredBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        MeasuredBlock[] materialized = blocks.ToArray();

        string? duplicate = materialized
            .GroupBy(block => block.BlockId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Measured block ID '{duplicate}' is duplicated.",
                nameof(blocks));
        }

        Blocks = Array.AsReadOnly(materialized);
        _byId = materialized.ToDictionary(
            block => block.BlockId,
            StringComparer.Ordinal);
    }

    private readonly IReadOnlyDictionary<string, MeasuredBlock> _byId;

    public IReadOnlyList<MeasuredBlock> Blocks { get; }

    public MeasuredBlock GetBlock(string blockId)
    {
        if (!_byId.TryGetValue(blockId, out MeasuredBlock? block))
        {
            throw new KeyNotFoundException(
                $"Measured block '{blockId}' does not exist.");
        }

        return block;
    }
}

public sealed class CompositionMeasurer
{
    private readonly ITextMetrics _textMetrics;

    public CompositionMeasurer(ITextMetrics textMetrics)
    {
        _textMetrics = textMetrics ??
            throw new ArgumentNullException(nameof(textMetrics));
    }

    public CompositionMeasurement Measure(
        DrawingComposition composition,
        RIC18DrawingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(profile);

        Dictionary<string, BlockDefinition> blockDefinitions =
            profile.Blocks.ToDictionary(
                block => block.Id,
                StringComparer.Ordinal);
        Dictionary<string, SymbolDefinition> symbolDefinitions =
            profile.Symbols.ToDictionary(
                symbol => symbol.Id,
                StringComparer.Ordinal);
        Dictionary<string, TextStyleDefinition> textStyles =
            profile.TextStyles.ToDictionary(
                style => style.Id,
                StringComparer.Ordinal);

        MeasuredBlock[] measured = composition.Blocks
            .OrderBy(block => block.Id, StringComparer.Ordinal)
            .Select(block => MeasureBlock(
                block,
                profile.Layout,
                blockDefinitions,
                symbolDefinitions,
                textStyles))
            .ToArray();

        return new CompositionMeasurement(measured);
    }

    private MeasuredBlock MeasureBlock(
        CompositionBlock block,
        LayoutProfile layout,
        IReadOnlyDictionary<string, BlockDefinition> blockDefinitions,
        IReadOnlyDictionary<string, SymbolDefinition> symbols,
        IReadOnlyDictionary<string, TextStyleDefinition> textStyles)
    {
        if (!blockDefinitions.TryGetValue(
                block.BlockDefinitionId,
                out BlockDefinition? definition))
        {
            throw new InvalidOperationException(
                $"Drawing profile is missing block definition '{block.BlockDefinitionId}'.");
        }

        double width = definition.MinimumSize.Width;
        double height = definition.MinimumSize.Height;
        var labelMeasurements =
            new Dictionary<string, TextMeasurement>(StringComparer.Ordinal);

        foreach (BlockPartDefinition part in definition.Parts)
        {
            string symbolId = block.SymbolOverrides.TryGetValue(
                part.Id,
                out string? overrideSymbolId)
                    ? overrideSymbolId
                    : part.SymbolId;

            if (!symbols.TryGetValue(symbolId, out SymbolDefinition? symbol))
            {
                throw new InvalidOperationException(
                    $"Drawing profile is missing symbol definition '{symbolId}'.");
            }

            width = Math.Max(
                width,
                part.Offset.X +
                symbol.NominalBounds.X +
                symbol.NominalBounds.Width);
            height = Math.Max(
                height,
                part.Offset.Y +
                symbol.NominalBounds.Y +
                symbol.NominalBounds.Height);

            MeasurePartLabel(
                block,
                part,
                symbol,
                layout,
                textStyles,
                labelMeasurements,
                ref width,
                ref height);
        }

        return new MeasuredBlock(
            block.Id,
            new MmSize(width, height),
            labelMeasurements,
            definition.Parts.Count);
    }

    private void MeasurePartLabel(
        CompositionBlock block,
        BlockPartDefinition part,
        SymbolDefinition symbol,
        LayoutProfile layout,
        IReadOnlyDictionary<string, TextStyleDefinition> textStyles,
        IDictionary<string, TextMeasurement> labelMeasurements,
        ref double width,
        ref double height)
    {
        if (part.LabelSlotId is null ||
            !block.Labels.TryGetValue(part.LabelSlotId, out string? text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        LabelSlot slot = symbol.LabelSlots
            .SingleOrDefault(label =>
                string.Equals(
                    label.Id,
                    part.LabelSlotId,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Symbol '{symbol.Id}' does not contain label slot '{part.LabelSlotId}'.");

        if (!textStyles.TryGetValue(
                slot.TextStyleId,
                out TextStyleDefinition? textStyle))
        {
            throw new InvalidOperationException(
                $"Drawing profile is missing text style '{slot.TextStyleId}'.");
        }

        TextMeasurement measurement =
            _textMetrics.Measure(text, textStyle);
        labelMeasurements[slot.Id] = measurement;

        double labelRight =
            part.Offset.X +
            slot.Bounds.X +
            measurement.WidthMm +
            layout.TextPaddingMm;
        double labelBottom =
            part.Offset.Y +
            slot.Bounds.Y +
            measurement.HeightMm +
            layout.TextPaddingMm;

        width = Math.Max(width, labelRight);
        height = Math.Max(height, labelBottom);
    }
}
