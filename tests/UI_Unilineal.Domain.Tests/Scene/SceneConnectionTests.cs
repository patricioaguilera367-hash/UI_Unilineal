using System.Reflection;
using UI_Unilineal.Domain.Connections;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Scene;

public sealed class SceneConnectionTests
{
    [Fact]
    public void SceneConnection_StoresAnchorReferencesInsteadOfEndpointCoordinates()
    {
        var source = new SceneAnchorRef(
            new SceneId("detail/B1/source"),
            "OUT");
        var target = new SceneAnchorRef(
            new SceneId("detail/B1/bus"),
            "IN");

        var connection = new SceneConnection(
            new SceneId("detail/B1/connection/source-bus"),
            source,
            target,
            "POWER",
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            new EntityReference(new EntityUid("SC1"), EntityKind.SupplyConnection));

        Assert.Equal(source, connection.Source);
        Assert.Equal(target, connection.Target);

        PropertyInfo[] properties = typeof(SceneAnchorRef).GetProperties();
        Assert.DoesNotContain(
            properties,
            property => property.PropertyType == typeof(MmPoint));
    }

    [Fact]
    public void SceneElement_PreservesSemanticAnchorRoles()
    {
        var anchor = new SceneAnchor(
            "GROUND",
            AnchorRole.Ground,
            new MmPoint(20, 8),
            AnchorDirection.Down);

        var group = new GroupSceneElement(
            new SceneId("detail/B1/bus"),
            new MmRect(0, 0, 40, 10),
            SceneLayer.Symbol,
            20,
            SceneVisibility.Both,
            new EntityReference(new EntityUid("BUS:B1:MAIN"), EntityKind.Bus),
            null,
            [],
            [anchor]);

        SceneAnchor stored = Assert.Single(group.Anchors);
        Assert.Equal(AnchorRole.Ground, stored.Role);
        Assert.Equal(new MmPoint(20, 8), stored.Point);
    }

    [Fact]
    public void DiagramScene_DefensivelyCopiesConnections()
    {
        var connection = new SceneConnection(
            new SceneId("summary/connection/SC1"),
            new SceneAnchorRef(new SceneId("summary/source/S1"), "OUT"),
            new SceneAnchorRef(new SceneId("summary/board/B1"), "IN"),
            "POWER",
            SceneLayer.Power,
            10,
            SceneVisibility.Both,
            null);
        var connections = new List<SceneConnection> { connection };

        var scene = new DiagramScene(
            new MmRect(0, 0, 100, 100),
            [],
            new DiagramSceneMetadata(
                "RIC18-V1",
                "1.0.0",
                "PROFILE",
                "G4",
                "INPUT",
                "PROJECTION"),
            connections);

        connections.Clear();

        Assert.Single(scene.Connections);
    }
}
