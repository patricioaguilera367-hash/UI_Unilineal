using System.Reflection;
using System.Text.Json;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Scene;

public sealed class DiagramLayoutStateTests
{
    [Fact]
    public void LayoutState_PublicDataIsPresentationOnly()
    {
        string[] allowedProperties =
        [
            "SceneKind",
            "ScopeUid",
            "Version",
            "Overrides",
            "ViewportPreference"
        ];

        string[] actual = typeof(DiagramLayoutState)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            allowedProperties.OrderBy(name => name, StringComparer.Ordinal),
            actual);

        var state = new DiagramLayoutState(
            DiagramSceneKind.BoardDetail,
            new EntityUid("B1"),
            "1",
            [
                new LayoutOverride(
                    new EntityUid("C1"),
                    new MmPoint(10, 20),
                    LayoutLockMode.Pinned)
            ],
            new DiagramViewportPreference(
                1.25,
                new MmPoint(100, 80)));

        string json = JsonSerializer.Serialize(state);

        Assert.DoesNotContain("Circuit", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Protection", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Conductor", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SupplyConnection", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Topology", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LayoutState_DefensivelyCopiesOverrides()
    {
        var overrides = new List<LayoutOverride>
        {
            new(
                new EntityUid("B1"),
                new MmPoint(10, 10),
                LayoutLockMode.Locked)
        };

        var state = new DiagramLayoutState(
            DiagramSceneKind.ProjectSummary,
            new EntityUid("P1"),
            "1",
            overrides,
            null);

        overrides.Clear();

        Assert.Single(state.Overrides);
    }

    [Fact]
    public void LayoutState_RejectsDuplicateEntityOverrides()
    {
        Assert.Throws<ArgumentException>(
            () => new DiagramLayoutState(
                DiagramSceneKind.BoardDetail,
                new EntityUid("B1"),
                "1",
                [
                    new LayoutOverride(
                        new EntityUid("C1"),
                        new MmPoint(10, 10),
                        LayoutLockMode.Pinned),
                    new LayoutOverride(
                        new EntityUid("C1"),
                        new MmPoint(20, 20),
                        LayoutLockMode.Locked)
                ],
                null));
    }

    [Theory]
    [InlineData(LayoutLockMode.Auto)]
    [InlineData(LayoutLockMode.Pinned)]
    [InlineData(LayoutLockMode.Locked)]
    public void LayoutOverride_AcceptsAllDefinedLockModes(LayoutLockMode mode)
    {
        var value = new LayoutOverride(
            new EntityUid("B1"),
            new MmPoint(10, 10),
            mode);

        Assert.Equal(mode, value.LockMode);
    }
}
