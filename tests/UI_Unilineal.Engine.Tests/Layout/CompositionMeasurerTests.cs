using System.Globalization;
using UI_Unilineal.Domain.Blocks;
using UI_Unilineal.Domain.Profiles;
using UI_Unilineal.Domain.Scene;
using UI_Unilineal.Engine.Composition;
using UI_Unilineal.Engine.Layout;
using UI_Unilineal.Engine.Projection;

namespace UI_Unilineal.Engine.Tests.Layout;

public sealed class CompositionMeasurerTests
{
    [Fact]
    public void DeterministicTextMetrics_IsStableAcrossCultures()
    {
        RIC18DrawingProfile profile = Profile();
        TextStyleDefinition style = profile.TextStyles.Single(x => x.Id == "TECH");
        var metrics = new DeterministicTextMetrics();

        TextMeasurement first;
        TextMeasurement second;

        using (new CultureScope("es-CL"))
        {
            first = metrics.Measure("TDA-01 — Alumbrado 12,5 kW", style);
        }

        using (new CultureScope("de-DE"))
        {
            second = metrics.Measure("TDA-01 — Alumbrado 12,5 kW", style);
        }

        Assert.Equal(first, second);
        Assert.True(first.WidthMm > 0);
        Assert.Equal(style.HeightMm, first.HeightMm);
    }

    [Fact]
    public void Measure_LongLabelCanGrowBlockBeyondMinimumWidth()
    {
        RIC18DrawingProfile profile = Profile();
        var block = new CompositionBlock(
            "detail/B1/branch/C1/protection/PR1",
            "PROTECTION_CHAIN_BLOCK",
            "Protection",
            null,
            new Dictionary<string, string>
            {
                ["RATING"] = "Interruptor termomagnético 125 A curva C 25 kA"
            },
            ProjectionStatus.Ok,
            null,
            new Dictionary<string, string>
            {
                ["PROTECTION"] = "BREAKER"
            });
        DrawingComposition composition = Composition(profile, [block]);

        CompositionMeasurement measurement =
            new CompositionMeasurer(new DeterministicTextMetrics())
                .Measure(composition, profile);

        MeasuredBlock measured = Assert.Single(measurement.Blocks);

        Assert.True(
            measured.Size.Width >
            profile.Blocks.Single(x => x.Id == "PROTECTION_CHAIN_BLOCK")
                .MinimumSize.Width);
        Assert.True(measured.Labels["RATING"].WidthMm > 0);
    }

    [Fact]
    public void Measure_BlockWithMorePartsReflectsActualPartExtents()
    {
        RIC18DrawingProfile source = Profile();
        BlockDefinition original =
            source.Blocks.Single(x => x.Id == "PROTECTION_CHAIN_BLOCK");
        var expanded = new BlockDefinition(
            original.Id,
            original.SemanticRole,
            original.MinimumSize,
            [
                new BlockPartDefinition(
                    "P1",
                    "BREAKER",
                    new MmPoint(4, 4),
                    "RATING"),
                new BlockPartDefinition(
                    "P2",
                    "RCD",
                    new MmPoint(4, 28),
                    "RATING")
            ],
            original.ProvenanceIds);
        var profile = new RIC18DrawingProfile(
            source.ProfileId,
            source.Version,
            source.SourceDocument,
            source.Provenance,
            source.Symbols,
            source.Blocks.Select(x => x.Id == expanded.Id ? expanded : x),
            source.LineStyles,
            source.TextStyles,
            source.Layout);
        var block = new CompositionBlock(
            "detail/B1/branch/C1/protection/CHAIN",
            "PROTECTION_CHAIN_BLOCK",
            "Protection",
            null,
            new Dictionary<string, string>
            {
                ["RATING"] = "30 mA"
            },
            ProjectionStatus.Ok,
            null);
        DrawingComposition composition = Composition(profile, [block]);

        CompositionMeasurement measurement =
            new CompositionMeasurer(new DeterministicTextMetrics())
                .Measure(composition, profile);

        MeasuredBlock measured = Assert.Single(measurement.Blocks);

        Assert.True(measured.Size.Height >= 44);
        Assert.Equal(2, measured.PartCount);
    }

    [Fact]
    public void Measure_ShuffledBlocks_ReturnsStableOrderByCompositionId()
    {
        RIC18DrawingProfile profile = Profile();
        CompositionBlock a = Block("summary/board/B2");
        CompositionBlock b = Block("summary/board/B1");

        CompositionMeasurement first =
            new CompositionMeasurer(new DeterministicTextMetrics())
                .Measure(Composition(profile, [a, b]), profile);
        CompositionMeasurement second =
            new CompositionMeasurer(new DeterministicTextMetrics())
                .Measure(Composition(profile, [b, a]), profile);

        Assert.Equal(
            first.Blocks.Select(x => (x.BlockId, x.Size)),
            second.Blocks.Select(x => (x.BlockId, x.Size)));
    }

    private static CompositionBlock Block(string id) =>
        new(
            id,
            "BOARD_SUMMARY_BLOCK",
            "BoardSummary",
            null,
            new Dictionary<string, string>
            {
                ["NAME"] = id
            },
            ProjectionStatus.Ok,
            null);

    private static DrawingComposition Composition(
        RIC18DrawingProfile profile,
        IEnumerable<CompositionBlock> blocks) =>
        new(
            DrawingCompositionKind.BoardDetail,
            null,
            blocks,
            [],
            "INPUT",
            DrawingProfileFingerprint.Compute(profile));

    private static RIC18DrawingProfile Profile() =>
        new Ric18DrawingProfileLoader().LoadDirectory(
            Path.Combine(AppContext.BaseDirectory, "ProfileData"));

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture =
            CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture =
            CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }
}
