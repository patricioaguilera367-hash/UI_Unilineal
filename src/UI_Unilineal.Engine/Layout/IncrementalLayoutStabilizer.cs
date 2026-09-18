using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;

namespace UI_Unilineal.Engine.Layout;

public sealed class IncrementalLayoutStabilizer
{
    public PositionedLayout Stabilize(
        PositionedLayout previousLayout,
        PositionedLayout proposedLayout,
        DrawingComposition newComposition,
        DiagramLayoutState layoutState,
        LayoutProfile profile)
    {
        ArgumentNullException.ThrowIfNull(previousLayout);
        ArgumentNullException.ThrowIfNull(proposedLayout);
        ArgumentNullException.ThrowIfNull(newComposition);
        ArgumentNullException.ThrowIfNull(layoutState);
        ArgumentNullException.ThrowIfNull(profile);

        HashSet<string> compositionIds = newComposition.Blocks
            .Select(block => block.Id)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> proposedIds = proposedLayout.Blocks
            .Select(block => block.BlockId)
            .ToHashSet(StringComparer.Ordinal);

        if (!compositionIds.SetEquals(proposedIds))
        {
            throw new InvalidOperationException(
                "Proposed layout must contain exactly the blocks in the new composition.");
        }

        Dictionary<string, PositionedCompositionBlock> previous =
            previousLayout.Blocks.ToDictionary(
                block => block.BlockId,
                StringComparer.Ordinal);
        Dictionary<string, PositionedCompositionBlock> proposed =
            proposedLayout.Blocks.ToDictionary(
                block => block.BlockId,
                StringComparer.Ordinal);

        var result =
            new Dictionary<string, PositionedCompositionBlock>(
                StringComparer.Ordinal);
        var priorityIds =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (string id in compositionIds
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            if (previous.TryGetValue(
                    id,
                    out PositionedCompositionBlock? existing))
            {
                result.Add(id, existing);
                priorityIds.Add(id);
            }
            else
            {
                result.Add(id, proposed[id]);
            }
        }

        ApplyNewPriorityOverrides(
            result,
            previous.Keys.ToHashSet(StringComparer.Ordinal),
            newComposition,
            layoutState,
            profile,
            priorityIds);

        PositionedLayout merged = new(
            result.Values,
            proposedLayout.Bounds);

        return new CollisionResolver().Resolve(
            merged,
            profile,
            priorityIds);
    }

    private static void ApplyNewPriorityOverrides(
        IDictionary<string, PositionedCompositionBlock> positions,
        IReadOnlySet<string> previousIds,
        DrawingComposition composition,
        DiagramLayoutState state,
        LayoutProfile profile,
        ISet<string> priorityIds)
    {
        Dictionary<string, CompositionBlock> entityIndex =
            composition.Blocks
                .Where(block => block.Entity is not null)
                .GroupBy(
                    block => block.Entity!.Uid.Value,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        CompositionBlock[] blocks = group.ToArray();

                        if (blocks.Length != 1)
                        {
                            throw new InvalidOperationException(
                                $"Entity '{group.Key}' has ambiguous composition blocks.");
                        }

                        return blocks[0];
                    },
                    StringComparer.Ordinal);

        foreach (LayoutOverride value in state.Overrides
                     .Where(value =>
                         value.LockMode != LayoutLockMode.Auto)
                     .OrderBy(
                         value => value.EntityUid.Value,
                         StringComparer.Ordinal))
        {
            if (!entityIndex.TryGetValue(
                    value.EntityUid.Value,
                    out CompositionBlock? block) ||
                previousIds.Contains(block.Id) ||
                !positions.TryGetValue(
                    block.Id,
                    out PositionedCompositionBlock? current))
            {
                continue;
            }

            MmRect candidate = new(
                value.Position.X,
                value.Position.Y,
                current.Bounds.Width,
                current.Bounds.Height);

            if (value.LockMode == LayoutLockMode.Pinned)
            {
                candidate = ResolvePinned(
                    candidate,
                    priorityIds
                        .Select(id => positions[id].Bounds)
                        .ToArray(),
                    profile);
            }

            positions[block.Id] =
                new PositionedCompositionBlock(
                    block.Id,
                    candidate);
            priorityIds.Add(block.Id);
        }
    }

    private static MmRect ResolvePinned(
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

            double y = collisions.Max(
                bounds =>
                    bounds.Bottom +
                    profile.VerticalGapMm);

            candidate = new MmRect(
                candidate.X,
                Math.Ceiling(y / profile.GridMm) *
                profile.GridMm,
                candidate.Width,
                candidate.Height);
        }
    }

    private static bool Overlaps(
        MmRect first,
        MmRect second) =>
        first.X < second.Right &&
        first.Right > second.X &&
        first.Y < second.Bottom &&
        first.Bottom > second.Y;
}
