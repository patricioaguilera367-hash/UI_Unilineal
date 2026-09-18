using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Scene;

public enum DiagramSceneKind
{
    ProjectSummary,
    BoardDetail
}

public enum LayoutLockMode
{
    Auto,
    Pinned,
    Locked
}

public sealed record LayoutOverride
{
    public LayoutOverride(
        EntityUid entityUid,
        MmPoint position,
        LayoutLockMode lockMode)
    {
        EntityUid = entityUid ??
            throw new ArgumentNullException(nameof(entityUid));
        Position = position;
        LockMode = lockMode;
    }

    public EntityUid EntityUid { get; }

    public MmPoint Position { get; }

    public LayoutLockMode LockMode { get; }
}

public sealed record DiagramViewportPreference
{
    public DiagramViewportPreference(
        double zoom,
        MmPoint center)
    {
        if (!double.IsFinite(zoom) || zoom <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }

        Zoom = zoom;
        Center = center;
    }

    public double Zoom { get; }

    public MmPoint Center { get; }
}

public sealed class DiagramLayoutState
{
    public DiagramLayoutState(
        DiagramSceneKind sceneKind,
        EntityUid scopeUid,
        string version,
        IEnumerable<LayoutOverride> overrides,
        DiagramViewportPreference? viewportPreference)
    {
        ScopeUid = scopeUid ??
            throw new ArgumentNullException(nameof(scopeUid));

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException(
                "Layout state version is required.",
                nameof(version));
        }

        ArgumentNullException.ThrowIfNull(overrides);

        LayoutOverride[] materialized = overrides.ToArray();

        string? duplicate = materialized
            .GroupBy(
                value => value.EntityUid.Value,
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicate is not null)
        {
            throw new ArgumentException(
                $"Layout override for entity '{duplicate}' is duplicated.",
                nameof(overrides));
        }

        SceneKind = sceneKind;
        Version = version;
        Overrides = Array.AsReadOnly(materialized);
        ViewportPreference = viewportPreference;
    }

    public DiagramSceneKind SceneKind { get; }

    public EntityUid ScopeUid { get; }

    public string Version { get; }

    public IReadOnlyList<LayoutOverride> Overrides { get; }

    public DiagramViewportPreference? ViewportPreference { get; }
}
