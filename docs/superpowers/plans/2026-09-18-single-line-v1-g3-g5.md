# Single-Line V1 G3–G5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the renderer-neutral graphic profile, composition, vector scene and deterministic layout pipeline that transforms a validated `SingleLineProjection` into a stable `DiagramScene` measured in millimetres.

**Architecture:** G3 adds immutable profile/symbol/block contracts in Domain and profile validation/loading/composition in Engine. G4 introduces the neutral vector scene with deterministic identities, semantic anchors and scene validation. G5 adds measurement, summary/detail placement, orthogonal routing, collision handling, presentation-only overrides and deterministic/stress evidence. No Avalonia type enters Domain or Engine.

**Tech Stack:** .NET 9, C# 13, System.Text.Json, xUnit 2.9.2, GitHub Actions Windows/Linux.

**Spec:** `docs/superpowers/specs/2026-09-16-single-line-v1-design.md`

**Baseline:** `f2394eb11d0599cf2e6550c9ecd11662d1ad18bd` / tag `g0-g2-green-2026-09-18`.

## Global Constraints

- Domain and Engine never reference Avalonia or ProyectoElectrico.
- Engine must continue to reference Domain; architecture guards remain green.
- One internal drawing unit equals one millimetre.
- Equivalent projection/profile/layout inputs must produce deterministic composition/scene output.
- Every profile rule has exactly one provenance classification: `RIC18_EXPLICIT`, `RIC18_CATALOG`, `RIC18_REFERENCE`, or `APP_CONVENTION`.
- No geometry introduced in this package may claim RIC authority without an authoritative citation already present in repository provenance data; initial uncited geometry is `APP_CONVENTION`.
- Composition decides **what** exists. Layout decides **where** it is placed.
- Renderers never calculate layout.
- Connections bind semantic anchors, never arbitrary endpoints.
- Structural electrical routes are orthogonal in V1.
- Manual layout state contains presentation metadata only; no electrical topology.
- TDD is mandatory: RED must be observed before production code for every behavior.
- After every task: `dotnet format --verify-no-changes`, Release build, full tests.
- G3–G5 closes only when Windows and Ubuntu CI are green, build has zero warnings, goldens are reviewed, `git diff --check` is clean and working tree is clean.

---

## File map locked for this package

### Domain
- `src/UI_Unilineal.Domain/Scene/Geometry.cs` — millimetre geometry value types.
- `src/UI_Unilineal.Domain/Profiles/GraphicRuleSource.cs` — provenance classifications.
- `src/UI_Unilineal.Domain/Profiles/DrawingStyles.cs` — line/text/layout style contracts.
- `src/UI_Unilineal.Domain/Profiles/RIC18DrawingProfile.cs` — immutable aggregate.
- `src/UI_Unilineal.Domain/Symbols/SymbolDefinition.cs` — symbol/primitive/anchor/label contracts.
- `src/UI_Unilineal.Domain/Blocks/BlockDefinition.cs` — semantic block definitions.
- `src/UI_Unilineal.Domain/Connections/AnchorRole.cs` — anchor/connection roles.
- `src/UI_Unilineal.Domain/Scene/DiagramScene.cs` and focused element files — neutral scene.
- `src/UI_Unilineal.Domain/Scene/DiagramLayoutState.cs` — presentation-only layout overrides.

### Engine
- `src/UI_Unilineal.Engine/Composition/DrawingProfileValidator.cs`
- `src/UI_Unilineal.Engine/Composition/Ric18DrawingProfileLoader.cs`
- `src/UI_Unilineal.Engine/Composition/DrawingProfileFingerprint.cs`
- `src/UI_Unilineal.Engine/Composition/DrawingComposition.cs`
- `src/UI_Unilineal.Engine/Composition/CompositionBuilder.cs`
- `src/UI_Unilineal.Engine/Layout/ITextMetrics.cs`
- `src/UI_Unilineal.Engine/Layout/DeterministicTextMetrics.cs`
- `src/UI_Unilineal.Engine/Layout/SummaryLayoutStrategy.cs`
- `src/UI_Unilineal.Engine/Layout/BoardDetailLayoutStrategy.cs`
- `src/UI_Unilineal.Engine/Layout/OrthogonalConnectionRouter.cs`
- `src/UI_Unilineal.Engine/Layout/CollisionResolver.cs`
- `src/UI_Unilineal.Engine/Layout/DiagramSceneValidator.cs`
- `src/UI_Unilineal.Engine/Layout/DiagramSceneFingerprint.cs`
- `src/UI_Unilineal.Engine/Layout/SingleLineLayoutEngine.cs`

### Data/tests
- `data/ric18/v1/*.json` — versioned renderer-neutral profile data.
- `tests/UI_Unilineal.Domain.Tests/{Profiles,Symbols,Scene}/...`
- `tests/UI_Unilineal.Engine.Tests/{Composition,Layout,Scene}/...`
- `tests/UI_Unilineal.Engine.Tests/Golden/{Composition,Scene}/...`

---

## G3 — Graphic profile and composition

### Task 1: Millimetre geometry and provenance contracts

**Files:**
- Create: `src/UI_Unilineal.Domain/Scene/Geometry.cs`
- Create: `src/UI_Unilineal.Domain/Profiles/GraphicRuleSource.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Scene/GeometryTests.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Profiles/GraphicRuleSourceTests.cs`

**Interfaces:**
- Produces: `MmPoint`, `MmSize`, `MmRect`, `GraphicRuleClassification`, `GraphicRuleSource`.

- [ ] **Step 1: RED — finite millimetre geometry**

```csharp
[Fact]
public void MmRect_RejectsNonPositiveSize()
{
    Assert.Throws<ArgumentOutOfRangeException>(
        () => new MmRect(0, 0, 0, 10));
}
```

- [ ] **Step 2: RED — provenance is explicit and nonblank**

```csharp
[Fact]
public void GraphicRuleSource_RejectsBlankDescription()
{
    Assert.Throws<ArgumentException>(() => new GraphicRuleSource(
        "APP:BASE",
        GraphicRuleClassification.APP_CONVENTION,
        null, null, null, null, " "));
}
```

- [ ] **Step 3: Run targeted Domain tests; expected failure because types do not exist.**

- [ ] **Step 4: GREEN — implement immutable geometry**

```csharp
public readonly record struct MmPoint(double X, double Y)
{
    public MmPoint
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y))
            throw new ArgumentOutOfRangeException(nameof(X));
    }
}

public readonly record struct MmSize(double Width, double Height)
{
    public MmSize
    {
        if (!double.IsFinite(Width) || !double.IsFinite(Height) ||
            Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
    }
}

public readonly record struct MmRect(double X, double Y, double Width, double Height)
{
    public MmRect
    {
        if (!double.IsFinite(X) || !double.IsFinite(Y) ||
            !double.IsFinite(Width) || !double.IsFinite(Height) ||
            Width <= 0 || Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(Width));
    }

    public double Right => X + Width;
    public double Bottom => Y + Height;
}
```

- [ ] **Step 5: GREEN — implement provenance**

```csharp
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
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Required.", nameof(id));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Required.", nameof(description));
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
```

- [ ] **Step 6: GREEN all tests; commit `feat(domain): add drawing geometry and provenance contracts`.**

### Task 2: Symbols, primitives, anchors and label slots

**Files:**
- Create: `src/UI_Unilineal.Domain/Connections/AnchorRole.cs`
- Create: `src/UI_Unilineal.Domain/Symbols/SymbolDefinition.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Symbols/SymbolDefinitionTests.cs`

**Interfaces:**
- Produces: `AnchorRole`, `AnchorDirection`, `SymbolPrimitive` hierarchy, `AnchorDefinition`, `LabelSlot`, `SymbolDefinition`.

- [ ] **Step 1: RED — symbol preserves vector primitives and semantic anchors.**

```csharp
[Fact]
public void SymbolDefinition_ExposesPowerAnchorsAndVectorPrimitives()
{
    var symbol = SymbolFixture.Breaker();
    Assert.Contains(symbol.Anchors, x => x.Role == AnchorRole.PowerIn);
    Assert.Contains(symbol.Anchors, x => x.Role == AnchorRole.PowerOut);
    Assert.Contains(symbol.Primitives, x => x is LineSymbolPrimitive);
}
```

- [ ] **Step 2: RED — duplicate anchor IDs are rejected by constructor-level local invariant.**

```csharp
[Fact]
public void SymbolDefinition_RejectsDuplicateAnchorIds()
{
    AnchorDefinition a = new("P", AnchorRole.PowerIn, new MmPoint(0, 0), AnchorDirection.Left);
    Assert.Throws<ArgumentException>(() => new SymbolDefinition(
        "BREAKER", "Breaker", new MmRect(0,0,10,10), [a,a], [], [], ["APP:BASE"]));
}
```

- [ ] **Step 3: Observe RED.**

- [ ] **Step 4: GREEN — implement enums and records. Primitive hierarchy contains exactly: line, polyline, rectangle, circle, arc, path. Each primitive uses style IDs rather than renderer types.**

```csharp
public enum AnchorRole { PowerIn, PowerOut, BusTap, Ground, Reference, Annotation }
public enum AnchorDirection { Left, Right, Up, Down, Any }

public abstract record SymbolPrimitive(string LineStyleId);
public sealed record LineSymbolPrimitive(MmPoint Start, MmPoint End, string StyleId)
    : SymbolPrimitive(StyleId);

public sealed record AnchorDefinition(
    string Id, AnchorRole Role, MmPoint Point, AnchorDirection Direction);

public sealed record LabelSlot(
    string Id, MmRect Bounds, int Priority, bool Required, string TextStyleId);
```

- [ ] **Step 5: GREEN full suite; commit `feat(domain): add vector symbol contracts`.**

### Task 3: Styles, layout profile and block definitions

**Files:**
- Create: `src/UI_Unilineal.Domain/Profiles/DrawingStyles.cs`
- Create: `src/UI_Unilineal.Domain/Blocks/BlockDefinition.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Profiles/DrawingStyleTests.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Blocks/BlockDefinitionTests.cs`

**Interfaces:**
- Produces: `LineSemanticRole`, `TextStyleDefinition`, `LineStyleDefinition`, `LayoutProfile`, `BlockDefinition`, `BlockPartDefinition`.

- [ ] **Step 1: RED — LayoutProfile rejects zero/negative technical spacing.**
- [ ] **Step 2: RED — BlockDefinition stores composition parts and required provenance IDs.**
- [ ] **Step 3: Observe RED.**
- [ ] **Step 4: GREEN with explicit properties: grid, block gaps, branch gaps, route clearance, text padding, max board-detail width, continuation-row gap.**

```csharp
public sealed record LayoutProfile(
    double GridMm,
    double HorizontalGapMm,
    double VerticalGapMm,
    double BranchGapMm,
    double RouteClearanceMm,
    double TextPaddingMm,
    double MaxBoardDetailWidthMm,
    double ContinuationRowGapMm);
```

```csharp
public sealed record BlockPartDefinition(
    string Id,
    string SymbolId,
    MmPoint Offset,
    string? LabelSlotId);

public sealed record BlockDefinition(
    string Id,
    string SemanticRole,
    MmSize MinimumSize,
    IReadOnlyList<BlockPartDefinition> Parts,
    IReadOnlyList<string> ProvenanceIds);
```

- [ ] **Step 5: GREEN; commit `feat(domain): define drawing styles layout profile and blocks`.**

### Task 4: Immutable RIC18 profile aggregate and validator

**Files:**
- Create: `src/UI_Unilineal.Domain/Profiles/RIC18DrawingProfile.cs`
- Create: `src/UI_Unilineal.Engine/Composition/DrawingProfileValidator.cs`
- Create: `src/UI_Unilineal.Engine/Composition/ProfileValidationIssue.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/DrawingProfileValidatorTests.cs`

**Interfaces:**
- Produces: immutable `RIC18DrawingProfile`; `DrawingProfileValidator.Validate(profile)`.

- [ ] **Step 1: RED — duplicate symbol/style/block/provenance IDs produce errors.**
- [ ] **Step 2: RED — missing style references, anchor outside nominal bounds, block missing symbol and missing provenance produce errors.**
- [ ] **Step 3: RED — every rule must have provenance; `APP_CONVENTION` may omit regulatory citation, RIC classifications require document + section/annex/figure locator.**
- [ ] **Step 4: Observe RED.**
- [ ] **Step 5: GREEN — aggregate defensively copies all collections and validator returns deterministically sorted typed issues.**

```csharp
public sealed class RIC18DrawingProfile
{
    public string ProfileId { get; }
    public string Version { get; }
    public IReadOnlyList<GraphicRuleSource> Provenance { get; }
    public IReadOnlyList<SymbolDefinition> Symbols { get; }
    public IReadOnlyList<BlockDefinition> Blocks { get; }
    public IReadOnlyList<LineStyleDefinition> LineStyles { get; }
    public IReadOnlyList<TextStyleDefinition> TextStyles { get; }
    public LayoutProfile Layout { get; }
}
```

- [ ] **Step 6: Add safety tests named `EveryGraphicRuleHasProvenance` and `NoAppConventionClaimsRicExplicit`.**
- [ ] **Step 7: GREEN; commit `feat(engine): validate versioned drawing profiles`.**

### Task 5: JSON loader and initial versioned profile data

**Files:**
- Create: `src/UI_Unilineal.Engine/Composition/Ric18DrawingProfileLoader.cs`
- Create: `data/ric18/v1/profile.json`
- Create: `data/ric18/v1/provenance.json`
- Create: `data/ric18/v1/line-styles.json`
- Create: `data/ric18/v1/text-styles.json`
- Create: `data/ric18/v1/layout.json`
- Create: `data/ric18/v1/symbols.json`
- Create: `data/ric18/v1/blocks.json`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/Ric18DrawingProfileLoaderTests.cs`

**Interfaces:**
- Produces: `Ric18DrawingProfileLoader.LoadDirectory(string path)`.

- [ ] **Step 1: RED — loader reads a complete directory and rejects missing required files.**
- [ ] **Step 2: RED — loaded profile passes `DrawingProfileValidator`.**
- [ ] **Step 3: Observe RED.**
- [ ] **Step 4: GREEN loader using `System.Text.Json` with `JsonStringEnumConverter`, case-sensitive profile IDs and explicit DTO-to-domain conversion.**
- [ ] **Step 5: Populate initial symbol IDs: `SOURCE_UTILITY`, `SERVICE_ENTRANCE`, `BREAKER`, `RCD`, `FUSE`, `BUS`, `GROUND`, `CONNECTION_NODE`, `DOWNSTREAM_BOARD`, `FINAL_LOAD`, `UNKNOWN_ENDPOINT`.**
- [ ] **Step 6: Populate initial block IDs: `SOURCE_BLOCK`, `BOARD_SUMMARY_BLOCK`, `INCOMING_SUPPLY_BLOCK`, `MAIN_PROTECTION_BLOCK`, `MAIN_BUS_BLOCK`, `CIRCUIT_BRANCH_BLOCK`, `PROTECTION_CHAIN_BLOCK`, `DOWNSTREAM_BOARD_BLOCK`, `FINAL_LOAD_BLOCK`, `GROUNDING_BLOCK`, `UNKNOWN_BLOCK`.**
- [ ] **Step 7: Classify every initial geometric/style rule as `APP_CONVENTION` with description `Initial application drawing convention; no regulatory geometry claim.` unless an existing repository citation proves a stronger classification.**
- [ ] **Step 8: GREEN; commit `feat(profile): add loadable RIC18 v1 drawing profile`.**

### Task 6: Profile fingerprint and gallery contract

**Files:**
- Create: `src/UI_Unilineal.Engine/Composition/DrawingProfileFingerprint.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/DrawingProfileFingerprintTests.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/SymbolGalleryContractTests.cs`

**Interfaces:**
- Produces deterministic SHA-256 `DrawingProfileFingerprint.Compute(profile)`.

- [ ] **Step 1: RED — shuffled profile collections produce identical fingerprint.**
- [ ] **Step 2: RED — changing one primitive/style/layout value changes fingerprint.**
- [ ] **Step 3: RED — gallery test enumerates every symbol and requires finite bounds, anchors within bounds, existing line/text style IDs and provenance.**
- [ ] **Step 4: GREEN canonical serializer with complete tie-breakers.**
- [ ] **Step 5: GREEN; commit `feat(profile): fingerprint and verify symbol catalog`.**

### Task 7: Drawing composition contracts and deterministic IDs

**Files:**
- Create: `src/UI_Unilineal.Engine/Composition/DrawingComposition.cs`
- Create: `src/UI_Unilineal.Engine/Composition/CompositionId.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/DrawingCompositionTests.cs`

**Interfaces:**
- Produces: `DrawingComposition`, `CompositionBlock`, `CompositionConnection`, `CompositionAnchorRef`, deterministic `CompositionIdFactory`.

- [ ] **Step 1: RED — identical semantic scope produces same block/connection IDs across runs.**
- [ ] **Step 2: RED — composition contains no coordinates, Avalonia types or mutable electrical data.**
- [ ] **Step 3: Observe RED.**
- [ ] **Step 4: GREEN IDs are path-based, e.g. `summary/source/S1`, `detail/B1/branch/C1`, `detail/B1/branch/C1/protection/PR1`.**
- [ ] **Step 5: GREEN; commit `feat(engine): define deterministic drawing composition`.**

### Task 8: Summary composition builder

**Files:**
- Create: `src/UI_Unilineal.Engine/Composition/CompositionBuilder.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/SummaryCompositionBuilderTests.cs`

**Interfaces:**
- Consumes `SingleLineProjection` + `RIC18DrawingProfile`.
- Produces summary `DrawingComposition`.

- [ ] **Step 1: RED — minimal projection becomes source block + board-summary block + one semantic power connection.**
- [ ] **Step 2: RED — alternate supply remains a second connection; it does not duplicate board block.**
- [ ] **Step 3: RED — unknown/missing semantic endpoint chooses `UNKNOWN_BLOCK`, not exception.**
- [ ] **Step 4: GREEN composition only; no coordinates.**
- [ ] **Step 5: GREEN; commit `feat(engine): compose project summary drawing blocks`.**

### Task 9: Board-detail composition builder

**Files:**
- Modify: `src/UI_Unilineal.Engine/Composition/CompositionBuilder.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Composition/BoardDetailCompositionBuilderTests.cs`
- Golden: `tests/UI_Unilineal.Engine.Tests/Golden/Composition/{minimal-detail,nested-detail}.txt`

**Interfaces:**
- Produces board-detail composition: incoming → main protection → bus → variable branch chains → destination + grounding.

- [ ] **Step 1: RED — protection chain length is preserved exactly, never hardcoded TM+ID.**
- [ ] **Step 2: RED — downstream board destination uses `DOWNSTREAM_BOARD_BLOCK`; final circuit uses `FINAL_LOAD_BLOCK`.**
- [ ] **Step 3: RED — grounding composes independently onto Ground anchor role.**
- [ ] **Step 4: GREEN.**
- [ ] **Step 5: Add reviewed structural goldens and run twice.**
- [ ] **Step 6: Commit `feat(engine): compose board detail drawing blocks`.**

### Task 10: G3 checkpoint

- [ ] **Run full Windows/Linux CI.**
- [ ] **Verify profile safety tests, composition goldens and architecture guards.**
- [ ] **No profile validation error; zero warnings.**
- [ ] **Commit docs: `docs: mark V1 G3 graphic composition checkpoint`.**

---

## G4 — Neutral scene foundation

### Task 11: Scene identity, layers, elements and metadata

**Files:**
- Create: `src/UI_Unilineal.Domain/Scene/SceneId.cs`
- Create: `src/UI_Unilineal.Domain/Scene/SceneLayer.cs`
- Create: `src/UI_Unilineal.Domain/Scene/SceneElement.cs`
- Create: `src/UI_Unilineal.Domain/Scene/DiagramScene.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Scene/DiagramSceneTests.cs`

**Interfaces:**
- Produces primitive scene elements: line, polyline, rectangle, circle, path, text, symbol, group.

- [ ] **Step 1: RED — blank/random SceneId prohibited; deterministic string required.**
- [ ] **Step 2: RED — every element carries bounds, layer, z-order, visibility and optional semantic reference.**
- [ ] **Step 3: GREEN with layers `Background, Power, Protection, Grounding, Symbol, Text, Annotation, StatusOverlay, Interaction` and visibility `Print, Interactive, Both`.**
- [ ] **Step 4: GREEN; commit `feat(domain): add neutral vector scene model`.**

### Task 12: Scene anchors and connections

**Files:**
- Create: `src/UI_Unilineal.Domain/Scene/SceneAnchor.cs`
- Create: `src/UI_Unilineal.Domain/Scene/SceneConnection.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Scene/SceneConnectionTests.cs`

**Interfaces:**
- Produces anchor-based connection references.

- [ ] **Step 1: RED — connection stores source/target SceneId + AnchorId, not raw endpoint coordinates.**
- [ ] **Step 2: RED — semantic roles survive into scene anchors.**
- [ ] **Step 3: GREEN.**
- [ ] **Step 4: Commit `feat(domain): add semantic scene anchors and connections`.**

### Task 13: Scene validator and fingerprint

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/DiagramSceneValidator.cs`
- Create: `src/UI_Unilineal.Engine/Layout/DiagramSceneFingerprint.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Scene/DiagramSceneValidatorTests.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Scene/DiagramSceneFingerprintTests.cs`

**Interfaces:**
- Produces typed scene validation issues and deterministic fingerprint.

- [ ] **Step 1: RED matrix: duplicate IDs, NaN/infinite point, nonpositive bounds, missing anchor, orphan connection, scene bounds not containing element.**
- [ ] **Step 2: RED — shuffled element/connection collections fingerprint identically.**
- [ ] **Step 3: GREEN deterministic validation sort and canonical fingerprint.**
- [ ] **Step 4: Commit `feat(engine): validate and fingerprint neutral scenes`.**

### Task 14: Composition-to-scene factory boundary

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/SceneAssembly.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Scene/SceneAssemblyTests.cs`

**Interfaces:**
- `SceneAssembly` receives already-positioned block geometry and converts profile symbol primitives to neutral scene elements. It never decides positions.

- [ ] **Step 1: RED — one positioned breaker block expands to deterministic symbol/text/anchor scene elements.**
- [ ] **Step 2: RED — same positioned composition yields same SceneIds and same scene fingerprint.**
- [ ] **Step 3: GREEN; commit `feat(engine): assemble positioned composition into scene`.**

### Task 15: G4 checkpoint

- [ ] **Run architecture guards and full CI.**
- [ ] **Verify no Avalonia reference in Domain/Engine.**
- [ ] **Commit docs: `docs: mark V1 G4 neutral scene checkpoint`.**

---

## G5 — Deterministic layout

### Task 16: Text metrics and measurement contracts

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/ITextMetrics.cs`
- Create: `src/UI_Unilineal.Engine/Layout/DeterministicTextMetrics.cs`
- Create: `src/UI_Unilineal.Engine/Layout/CompositionMeasurer.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/CompositionMeasurerTests.cs`

**Interfaces:**
- Produces `TextMeasurement`, `MeasuredBlock`, deterministic test metrics.

- [ ] **Step 1: RED — repeated measurement is identical and independent of current culture.**
- [ ] **Step 2: RED — variable protection-chain block height/width reflects actual child count/labels.**
- [ ] **Step 3: GREEN; commit `feat(layout): add deterministic text and block measurement`.**

### Task 17: Summary layout strategy

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/ISingleLineLayoutStrategy.cs`
- Create: `src/UI_Unilineal.Engine/Layout/SummaryLayoutStrategy.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/SummaryLayoutStrategyTests.cs`

**Interfaces:**
- Produces positioned blocks for summary; primary active topology defines depth, supplementary supplies route later.

- [ ] **Step 1: RED — chain S→B1→B2 yields monotonically increasing electrical depth and no block overlap.**
- [ ] **Step 2: RED — emergency/alternate source does not change primary B1/B2 positions.**
- [ ] **Step 3: RED — shuffled composition produces identical positions.**
- [ ] **Step 4: GREEN; commit `feat(layout): place deterministic summary hierarchy`.**

### Task 18: Board-detail layout and branch wrapping

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/BoardDetailLayoutStrategy.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/BoardDetailLayoutStrategyTests.cs`

**Interfaces:**
- Produces incoming → main protection → bus → branch columns, wraps after `MaxBoardDetailWidthMm`.

- [ ] **Step 1: RED — main path order is vertical and branches begin from bus taps.**
- [ ] **Step 2: RED — 1/4/12/24/48/100 branch fixtures never overlap structural blocks.**
- [ ] **Step 3: RED — large width triggers continuation row; wrapping is deterministic.**
- [ ] **Step 4: GREEN; commit `feat(layout): place board detail branches with wrapping`.**

### Task 19: Orthogonal router and compatibility matrix

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/OrthogonalConnectionRouter.cs`
- Create: `src/UI_Unilineal.Engine/Layout/AnchorCompatibility.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/OrthogonalConnectionRouterTests.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/AnchorCompatibilityTests.cs`

**Interfaces:**
- Produces horizontal/vertical polylines, exhaustive small anchor-role matrix.

- [ ] **Step 1: RED exhaustive compatibility matrix: PowerOut→PowerIn yes, BusTap→PowerIn yes, Ground→Ground yes; PowerIn→PowerIn, Ground→PowerIn and Annotation→PowerIn no.**
- [ ] **Step 2: RED — every routed segment is horizontal or vertical.**
- [ ] **Step 3: RED — router avoids supplied block obstacles using `RouteClearanceMm`.**
- [ ] **Step 4: GREEN; commit `feat(layout): route orthogonal semantic connections`.**

### Task 20: Collision resolution and strict scene validation

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/CollisionResolver.cs`
- Modify: `src/UI_Unilineal.Engine/Layout/DiagramSceneValidator.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/CollisionResolverTests.cs`

**Interfaces:**
- Resolves text/wire and block collisions without moving locked items.

- [ ] **Step 1: RED — structural block overlaps are errors under strict validation.**
- [ ] **Step 2: RED — resolver moves auto items onto profile grid and preserves semantic order.**
- [ ] **Step 3: GREEN; commit `feat(layout): resolve structural drawing collisions`.**

### Task 21: Presentation-only layout state

**Files:**
- Create: `src/UI_Unilineal.Domain/Scene/DiagramLayoutState.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Scene/DiagramLayoutStateTests.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/LayoutOverrideTests.cs`

**Interfaces:**
- Produces `LayoutOverride`, `LayoutLockMode { Auto, Pinned, Locked }`, `DiagramLayoutState`.

- [ ] **Step 1: RED — state serializable data contains only scene/scope/version/overrides/view preference; no circuit protection conductor or supply topology fields.**
- [ ] **Step 2: RED — Pinned keeps preferred position unless collision makes it impossible; Locked never moves.**
- [ ] **Step 3: GREEN; commit `feat(layout): add presentation-only layout overrides`.**

### Task 22: Incremental stability

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/IncrementalLayoutStabilizer.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/IncrementalLayoutStabilizerTests.cs`

**Interfaces:**
- Consumes previous positioned layout + new composition + layout state.

- [ ] **Step 1: RED — adding one new branch keeps all unaffected locked/pinned positions exact.**
- [ ] **Step 2: RED — adding branch preserves unaffected auto positions whenever no collision requires change.**
- [ ] **Step 3: GREEN; commit `feat(layout): preserve stable incremental positions`.**

### Task 23: SingleLineLayoutEngine orchestration

**Files:**
- Create: `src/UI_Unilineal.Engine/Layout/SingleLineLayoutEngine.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Layout/SingleLineLayoutEngineTests.cs`
- Golden: `tests/UI_Unilineal.Engine.Tests/Golden/Scene/{minimal-summary,nested-summary,minimal-detail,nested-detail}.txt`

**Interfaces:**
- Public API: `LayoutSummary(projection, profile, layoutState?)`; `LayoutBoardDetail(projection, boardUid, profile, layoutState?)`.
- Pipeline: Measure → Place → Route → Resolve → Validate → Scene.

- [ ] **Step 1: RED — valid projection/profile produces valid finite scene with profile/input/scene metadata fingerprints.**
- [ ] **Step 2: RED — invalid profile returns typed failure before layout.**
- [ ] **Step 3: RED — identical input/profile/layout repeated twice produces identical scene fingerprint and golden.**
- [ ] **Step 4: GREEN orchestration.**
- [ ] **Step 5: Add reviewed scene goldens; run twice on Linux and once on Windows.**
- [ ] **Step 6: Commit `feat(layout): complete deterministic single-line layout engine`.**

### Task 24: Stress/property evidence and G3–G5 final gate

**Files:**
- Create: `tests/UI_Unilineal.Engine.Tests/Layout/GeneratedLayoutInvariantTests.cs`
- Modify: `README.md`
- Modify: `docs/architecture/README.md`

**Interfaces:** none new.

- [ ] **Step 1: Generate 1/4/12/24/48/100 circuit board details; assert finite scene, valid anchors, orthogonal connections, no strict structural overlaps.**
- [ ] **Step 2: Generate shuffled equivalent compositions; scene fingerprints must match.**
- [ ] **Step 3: Run `dotnet restore UI_Unilineal.sln`.**
- [ ] **Step 4: Run `dotnet format UI_Unilineal.sln --verify-no-changes --no-restore`.**
- [ ] **Step 5: Run `dotnet build UI_Unilineal.sln -c Release --no-restore`; require 0 warnings/errors.**
- [ ] **Step 6: Run `dotnet test UI_Unilineal.sln -c Release --no-build`; require all tests green.**
- [ ] **Step 7: Run `git diff --check`; require no output.**
- [ ] **Step 8: Verify Windows + Ubuntu CI GREEN on exact final SHA.**
- [ ] **Step 9: Review diff against spec §§9–13 and invariants; fix Critical/Important findings with regression tests before proceeding.**
- [ ] **Step 10: Update docs to state G3–G5 implemented and G6 next; commit `docs: mark V1 G3-G5 vector layout checkpoint`.**
- [ ] **Step 11: Tag only after clean final gate, using `g3-g5-green-2026-09-18`.**

## Self-review result

- Spec coverage: G3 profile/provenance/symbols/blocks/composition covered by Tasks 1–10; G4 scene/anchors/validation covered by Tasks 11–15; G5 measurement/layout/routing/collision/manual/incremental/wrapping covered by Tasks 16–24.
- Normative safeguard: initial unverified geometry is explicitly `APP_CONVENTION`; validator prevents silent promotion to RIC classifications.
- Determinism: profile, composition and scene fingerprints include complete canonical tie-breakers; shuffle tests exist at each transformation boundary.
- Architecture: no renderer or host dependency is introduced.
- Placeholder scan: no TODO/TBD/implement-later placeholders.
- Type consistency: the public flow remains `SingleLineProjection → DrawingComposition → SingleLineLayoutEngine → DiagramScene`.
