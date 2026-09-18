namespace UI_Unilineal.Domain.Profiles;

public enum GraphicRuleClassification
{
    RIC18_EXPLICIT,
    RIC18_CATALOG,
    RIC18_REFERENCE,
    APP_CONVENTION
}

public sealed record GraphicRuleSource
{
    public GraphicRuleSource(
        string id,
        GraphicRuleClassification classification,
        string? document,
        string? section,
        string? annex,
        string? figure,
        string description)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Graphic rule source ID is required.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Graphic rule source description is required.", nameof(description));
        }

        Id = id;
        Classification = classification;
        Document = document;
        Section = section;
        Annex = annex;
        Figure = figure;
        Description = description;
    }

    public string Id { get; }

    public GraphicRuleClassification Classification { get; }

    public string? Document { get; }

    public string? Section { get; }

    public string? Annex { get; }

    public string? Figure { get; }

    public string Description { get; }
}
