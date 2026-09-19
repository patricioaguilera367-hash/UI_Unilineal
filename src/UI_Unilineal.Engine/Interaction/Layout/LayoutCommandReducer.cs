using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Engine.Interaction.Layout;

public sealed class LayoutCommandReducer
{
    public LayoutCommandResult Apply(
        DiagramLayoutState state,
        ILayoutCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            MoveEntityCommand move =>
                MoveEntity(
                    state,
                    move),
            SetEntityLockModeCommand setLockMode =>
                SetEntityLockMode(
                    state,
                    setLockMode),
            ResetEntityPositionCommand resetEntity =>
                ResetEntityPosition(
                    state,
                    resetEntity),
            ResetBoardLayoutCommand resetBoard =>
                ResetBoardLayout(
                    state,
                    resetBoard),
            ResetAllLayoutCommand =>
                ResetAllLayout(state),
            _ => throw new NotSupportedException(
                $"Unsupported layout command '{command.GetType().Name}'.")
        };
    }

    private static LayoutCommandResult MoveEntity(
        DiagramLayoutState state,
        MoveEntityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command.EntityUid);

        LayoutOverride? existing =
            FindOverride(
                state,
                command.EntityUid);

        LayoutOverride replacement =
            existing is null
                ? new LayoutOverride(
                    command.EntityUid,
                    command.Position,
                    LayoutLockMode.Pinned)
                : new LayoutOverride(
                    command.EntityUid,
                    command.Position,
                    existing.LockMode);

        if (existing is not null &&
            existing.Position == replacement.Position &&
            existing.LockMode == replacement.LockMode)
        {
            return NoChange(state);
        }

        return Changed(
            state,
            ReplaceOverride(
                state,
                replacement));
    }

    private static LayoutCommandResult SetEntityLockMode(
        DiagramLayoutState state,
        SetEntityLockModeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command.EntityUid);

        LayoutOverride? existing =
            FindOverride(
                state,
                command.EntityUid);

        if (existing is null ||
            existing.LockMode == command.LockMode)
        {
            return NoChange(state);
        }

        LayoutOverride replacement =
            new(
                existing.EntityUid,
                existing.Position,
                command.LockMode);

        return Changed(
            state,
            ReplaceOverride(
                state,
                replacement));
    }

    private static LayoutCommandResult ResetEntityPosition(
        DiagramLayoutState state,
        ResetEntityPositionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command.EntityUid);

        LayoutOverride[] remaining =
            state.Overrides
                .Where(value =>
                    value.EntityUid != command.EntityUid)
                .ToArray();

        if (remaining.Length == state.Overrides.Count)
        {
            return NoChange(state);
        }

        return Changed(
            state,
            Rebuild(
                state,
                remaining));
    }

    private static LayoutCommandResult ResetBoardLayout(
        DiagramLayoutState state,
        ResetBoardLayoutCommand command)
    {
        ArgumentNullException.ThrowIfNull(command.BoardUid);

        if (state.SceneKind != DiagramSceneKind.BoardDetail ||
            state.ScopeUid != command.BoardUid ||
            state.Overrides.Count == 0)
        {
            return NoChange(state);
        }

        return Changed(
            state,
            Rebuild(
                state,
                []));
    }

    private static LayoutCommandResult ResetAllLayout(
        DiagramLayoutState state)
    {
        if (state.Overrides.Count == 0)
        {
            return NoChange(state);
        }

        return Changed(
            state,
            Rebuild(
                state,
                []));
    }

    private static LayoutOverride? FindOverride(
        DiagramLayoutState state,
        EntityUid entityUid) =>
        state.Overrides
            .SingleOrDefault(value =>
                value.EntityUid == entityUid);

    private static DiagramLayoutState ReplaceOverride(
        DiagramLayoutState state,
        LayoutOverride replacement)
    {
        LayoutOverride[] overrides =
            state.Overrides
                .Where(value =>
                    value.EntityUid != replacement.EntityUid)
                .Append(replacement)
                .OrderBy(
                    value => value.EntityUid.Value,
                    StringComparer.Ordinal)
                .ToArray();

        return Rebuild(
            state,
            overrides);
    }

    private static DiagramLayoutState Rebuild(
        DiagramLayoutState source,
        IEnumerable<LayoutOverride> overrides) =>
        new(
            source.SceneKind,
            source.ScopeUid,
            source.Version,
            overrides,
            source.ViewportPreference);

    private static LayoutCommandResult Changed(
        DiagramLayoutState previous,
        DiagramLayoutState current) =>
        new(
            current,
            previous,
            Changed: true);

    private static LayoutCommandResult NoChange(
        DiagramLayoutState state) =>
        new(
            state,
            state,
            Changed: false);
}
