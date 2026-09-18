using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Semantics;

public sealed class EntityUidTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new EntityUid(value));
    }

    [Fact]
    public void Equality_IsOrdinalAndValueBased()
    {
        Assert.Equal(new EntityUid("BOARD:A"), new EntityUid("BOARD:A"));
        Assert.NotEqual(new EntityUid("BOARD:A"), new EntityUid("board:a"));
    }

    [Fact]
    public void EntityReference_KeepsKindSeparateFromUid()
    {
        var uid = new EntityUid("A");

        Assert.NotEqual(
            new EntityReference(uid, EntityKind.Board),
            new EntityReference(uid, EntityKind.Circuit));
    }
}
