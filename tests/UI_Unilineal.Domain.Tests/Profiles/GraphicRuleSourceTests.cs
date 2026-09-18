using UI_Unilineal.Domain.Profiles;

namespace UI_Unilineal.Domain.Tests.Profiles;

public sealed class GraphicRuleSourceTests
{
    [Fact]
    public void Constructor_RejectsBlankId()
    {
        Assert.Throws<ArgumentException>(() => new GraphicRuleSource(
            " ",
            GraphicRuleClassification.APP_CONVENTION,
            null,
            null,
            null,
            null,
            "Initial application drawing convention."));
    }

    [Fact]
    public void Constructor_RejectsBlankDescription()
    {
        Assert.Throws<ArgumentException>(() => new GraphicRuleSource(
            "APP:BASE",
            GraphicRuleClassification.APP_CONVENTION,
            null,
            null,
            null,
            null,
            " "));
    }

    [Fact]
    public void AppConvention_DoesNotRequireRegulatoryCitation()
    {
        var source = new GraphicRuleSource(
            "APP:BASE",
            GraphicRuleClassification.APP_CONVENTION,
            null,
            null,
            null,
            null,
            "Initial application drawing convention.");

        Assert.Null(source.Document);
        Assert.Null(source.Section);
        Assert.Equal(
            GraphicRuleClassification.APP_CONVENTION,
            source.Classification);
    }
}
