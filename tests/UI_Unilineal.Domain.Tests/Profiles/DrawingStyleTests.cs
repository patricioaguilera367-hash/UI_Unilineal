using UI_Unilineal.Domain.Profiles;

namespace UI_Unilineal.Domain.Tests.Profiles;

public sealed class DrawingStyleTests
{
    [Fact]
    public void LayoutProfile_RejectsNonPositiveTechnicalSpacing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutProfile(
            0,
            12,
            12,
            8,
            3,
            1,
            260,
            20,
            "APP:LAYOUT"));
    }

    [Fact]
    public void LineStyle_RejectsNonPositiveWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineStyleDefinition(
            "POWER",
            LineSemanticRole.Power,
            0,
            LinePattern.Solid,
            "APP:LINE"));
    }

    [Fact]
    public void TextStyle_PreservesTechnicalDefinitionAndProvenance()
    {
        var style = new TextStyleDefinition(
            "TECH",
            "Arial",
            2.5,
            false,
            "APP:TEXT");

        Assert.Equal(2.5, style.HeightMm);
        Assert.Equal("APP:TEXT", style.ProvenanceId);
    }
}
