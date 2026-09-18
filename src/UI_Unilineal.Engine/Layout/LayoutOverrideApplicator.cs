using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class LayoutOverrideApplicator
{
    public PositionedLayout Apply(
        PositionedLayout automaticLayout,
        DrawingComposition composition,
        DiagramLayoutState state,
        LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(automaticLayout);
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);

        Dictionary<string, CompositionBlock> blockByEntity =
            BuildEntityIndex(composition);

        var positions = automaticLayout.Blocks
            .ToDictionary(
                block => block.BlockId,
                block => block,
                StringComparer.Ordinal);

        var lockedIds = new HashSet<string>(StringComparer.Ordinal);
        var pinned = new List<(string BlockId, LayoutOverride Override)>();

        foreach (LayoutOverride layoutOverride in state.Overrides
                     .OrderBy(
                         value => value.EntityUid.Value,
                         StringComparer.Ordinal))
        {
            if (!blockByEntity.TryGetValue(
                    layoutOverride.EntityUid.Value,
                    out CompositionBlock? block))
            {
                continue;
            }

            if (!positions.TryGetValue(
                    block.Id,
                    out PositionedCompositionBlock? current))
            {
                continue;
            }

            switch (layoutOverride.LockMode)
            {
                case LayoutLockMode.Auto:
                    break;

                case LayoutLockMode.Locked:
                    positions[block.Id] =
                        AtPosition(current, layoutOverride.Position);
                    lockedIds.Add(block.Id);
                    break;

                case LayoutLockMode.Pinned:
                    pinned.Add((block.Id, layoutOverride));
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported layout lock mode '{layoutOverride.LockMode}'.");
            }
        }

        var occupiedPriority = lockedIds
            .Select(id => positions[id])
            .OrderBy(block => block.BlockId, StringComparer.Ordinal)
            .ToList();

        foreach ((string blockId, LayoutOverride layoutOverride) in pinned
                     .OrderBy(item => item.BlockId, StringComparer.Ordinal))
        {
            PositionedCompositionBlock current = positions[blockId];
            PositionedCompositionBlock preferred =
                AtPosition(current, layoutOverride.Position);

            MmRect resolvedBounds = ResolvePinnedBounds(
                preferred.Bounds,
                occupiedPriority.Select(item => item.Bounds).ToArray(),
                profile);

            var resolved = new PositionedCompositionBlock(
                blockId,
                resolvedBounds);

            positions[blockId] = resolved;
            occupiedPriority.Add(resolved);
            lockedIds.Add(blockId);
        }

        PositionedLayout withOverrides = new(
            positions.Values,
            automaticLayout.Bounds);

        return new CollisionResolver().Resolve(
            withOverrides,
            profile,
            lockedIds);
    }

    private static Dictionary<string, CompositionBlock> BuildEntityIndex(
        DrawingComposition composition)
    {
        var result =
            new Dictionary<string, CompositionBlock>(
                StringComparer.Ordinal);

        foreach (CompositionBlock block in composition.Blocks
                     .Where(block => block.Entity is not null)
                     .OrderBy(block => block.Id, StringComparer.Ordinal))
        {
            string uid = block.Entity!.Uid.Value;

            if (!result.TryAdd(uid, block))
            {
                throw new InvalidOperationException(
                    $"Entity '{uid}' is represented by more than one composition block; " +
                    "an entity-scoped layout override would be ambiguous.");
            }
        }

        return result;
    }

    private static PositionedCompositionBlock AtPosition(
        PositionedCompositionBlock source,
        MmPoint position) =>
        new(
            source.BlockId,
            new MmRect(
                position.X,
                position.Y,
                source.Bounds.Width,
                source.Bounds.Height));

    private static MmRect ResolvePinnedBounds(
        MmRect preferred,
        IReadOnlyList<MmRect> occupied,
        LayoutProfile profile)
    {
        MmRect candidate = preferred;

        while (true)
        {
            MmRect[] collisions = occupied
                .Where(bounds => Overlaps(candidate, bounds))
                .OrderBy(bounds => bounds.Bottom)
                .ThenBy(bounds => bounds.X)
                .ToArray();

            if (collisions.Length == 0)
            {
                return candidate;
            }

            double nextY = collisions.Max(
                bounds =>
                    bounds.Bottom +
                    profile.VerticalGapMm);

            candidate = new MmRect(
                candidate.X,
                SnapUp(nextY, profile.GridMm),
                candidate.Width,
                candidate.Height);
        }
    }

    private static double SnapUp(
        double value,
        double grid) =>
        Math.Ceiling(value / grid) * grid;

    private static bool Overlaps(
        MmRect first,
        MmRect second) =>
        first.X < second.Right &&
        first.Right > second.X &&
        first.Y < second.Bottom &&
        first.Bottom > second.Y;
}
