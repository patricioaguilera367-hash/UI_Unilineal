using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;

namespace UI_Unilineal.Engine.Layout;

public sealed class CollisionResolver
{
    public PositionedLayout Resolve(
        PositionedLayout layout,
        LayoutProfile profile,
        IReadOnlySet<string>? lockedBlockIds = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(profile);

        lockedBlockIds ??=
            new HashSet<string>(StringComparer.Ordinal);

        HashSet<string> existingIds = layout.Blocks
            .Select(block => block.BlockId)
            .ToHashSet(StringComparer.Ordinal);

        string? unknownLocked = lockedBlockIds
            .Where(id => !existingIds.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (unknownLocked is not null)
        {
            throw new KeyNotFoundException(
                $"Locked block '{unknownLocked}' does not exist in the layout.");
        }

        var resolved =
            new Dictionary<string, PositionedCompositionBlock>(
                StringComparer.Ordinal);

        foreach (PositionedCompositionBlock locked in layout.Blocks
                     .Where(block => lockedBlockIds.Contains(block.BlockId))
                     .OrderBy(block => block.BlockId, StringComparer.Ordinal))
        {
            resolved.Add(locked.BlockId, locked);
        }

        foreach (PositionedCompositionBlock automatic in layout.Blocks
                     .Where(block => !lockedBlockIds.Contains(block.BlockId))
                     .OrderBy(block => block.BlockId, StringComparer.Ordinal))
        {
            MmRect bounds = SnapToGrid(
                automatic.Bounds,
                profile.GridMm);

            while (true)
            {
                PositionedCompositionBlock[] collisions = resolved.Values
                    .Where(block => Overlaps(bounds, block.Bounds))
                    .OrderBy(block => block.Bounds.Bottom)
                    .ThenBy(block => block.BlockId, StringComparer.Ordinal)
                    .ToArray();

                if (collisions.Length == 0)
                {
                    break;
                }

                double nextY = collisions.Max(
                    block =>
                        block.Bounds.Bottom +
                        profile.VerticalGapMm);

                bounds = new MmRect(
                    bounds.X,
                    SnapUp(nextY, profile.GridMm),
                    bounds.Width,
                    bounds.Height);
            }

            resolved.Add(
                automatic.BlockId,
                new PositionedCompositionBlock(
                    automatic.BlockId,
                    bounds));
        }

        PositionedCompositionBlock[] blocks = resolved.Values
            .OrderBy(block => block.BlockId, StringComparer.Ordinal)
            .ToArray();

        return new PositionedLayout(
            blocks,
            ComputeBounds(blocks, profile.GridMm));
    }

    private static MmRect SnapToGrid(
        MmRect bounds,
        double grid) =>
        new(
            SnapNearest(bounds.X, grid),
            SnapNearest(bounds.Y, grid),
            bounds.Width,
            bounds.Height);

    private static double SnapNearest(
        double value,
        double grid) =>
        Math.Round(
            value / grid,
            MidpointRounding.AwayFromZero) * grid;

    private static double SnapUp(
        double value,
        double grid) =>
        Math.Ceiling(value / grid) * grid;

    private static MmRect ComputeBounds(
        IReadOnlyList<PositionedCompositionBlock> blocks,
        double margin)
    {
        if (blocks.Count == 0)
        {
            throw new InvalidOperationException(
                "Cannot compute bounds for an empty positioned layout.");
        }

        double minX = Math.Min(
            0,
            blocks.Min(block => block.Bounds.X) - margin);
        double minY = Math.Min(
            0,
            blocks.Min(block => block.Bounds.Y) - margin);
        double maxRight =
            blocks.Max(block => block.Bounds.Right) + margin;
        double maxBottom =
            blocks.Max(block => block.Bounds.Bottom) + margin;

        return new MmRect(
            minX,
            minY,
            maxRight - minX,
            maxBottom - minY);
    }

    private static bool Overlaps(
        MmRect first,
        MmRect second) =>
        first.X < second.Right &&
        first.Right > second.X &&
        first.Y < second.Bottom &&
        first.Bottom > second.Y;
}
