# UI_Unilineal V1 G6 — Avalonia Renderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first production Avalonia renderer and playground workflow over the neutral G5 `DiagramScene`, with deterministic viewport transforms, culling, hit-testing and summary/detail navigation while preserving the Domain/Engine architecture boundary.

**Architecture:** `UI_Unilineal.Rendering.Avalonia` consumes `DiagramScene` and neutral Engine contracts; it never calculates electrical topology or owns host state. A single immediate-mode `SingleLineView : Control` renders the immutable scene through `DrawingContext`, using a viewport transform, scene spatial index, hit-testing and passive interaction overlays. The Playground owns navigation and demo orchestration; G7 will add command/mode state machines rather than embedding them in the renderer.

**Tech Stack:** .NET 9, Avalonia 11.3.18, Avalonia.Headless.XUnit 11.3.18, xUnit 2.9.2, existing UI_Unilineal Domain/Engine contracts.

**Spec:** `docs/superpowers/specs/2026-09-16-single-line-v1-design.md` §§14, 16 and the G6 boundary in the approved implementation roadmap.

## Global Constraints

- `UI_Unilineal.Domain` and `UI_Unilineal.Engine` must remain free of Avalonia and `ProyectoElectrico` references.
- Valid dependency direction for this package is `Rendering.Avalonia -> Engine -> Domain`; `Rendering.Avalonia` must not reference Playground or `ProyectoElectrico`.
- `DiagramScene` geometry remains in millimetres. Rendering transforms millimetres to Avalonia DIPs; it never mutates scene geometry.
- Base physical conversion is exactly `96.0 / 25.4` DIPs per millimetre before zoom.
- `SingleLineView` is one immediate-mode Avalonia `Control`; do not create one Avalonia control per scene primitive.
- Render order is deterministic by semantic layer, then `ZIndex`, then `SceneId`.
- Selection/hover visuals are overlays and must not be added to `DiagramScene`.
- G6 does not implement electrical commands, layout-command history, electrical reconnect gestures or G7 modes.
- G6 does not implement SVG/PDF/document composition.
- The Playground may use `DeterministicTextMetrics` to create reproducible demo scenes; the renderer itself must not own layout generation.
- Each task follows RED -> observed RED -> minimal GREEN -> focused tests -> commit.
- Final G6 gate requires Windows and Ubuntu CI GREEN, zero build warnings/errors, all tests GREEN, `git diff --check` clean and working tree clean.

---

## File map locked for G6

### Rendering.Avalonia

- `src/UI_Unilineal.Rendering.Avalonia/Viewport/ViewportState.cs` — immutable viewport data and zoom limits.
- `src/UI_Unilineal.Rendering.Avalonia/Viewport/ViewportTransform.cs` — scene-mm <-> viewport-DIP transforms.
- `src/UI_Unilineal.Rendering.Avalonia/Viewport/ViewportController.cs` — ZoomAt/Pan/Fit/ActualSize/CenterOn operations.
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/SceneSpatialIndex.cs` — renderer-neutral scene-mm uniform-grid index used for culling/hit candidates.
- `src/UI_Unilineal.Rendering.Avalonia/HitTesting/HitTestPolicy.cs` — hit tolerance and deterministic priority.
- `src/UI_Unilineal.Rendering.Avalonia/HitTesting/HitTestResult.cs` — scene hit result contract.
- `src/UI_Unilineal.Rendering.Avalonia/HitTesting/HitTestIndex.cs` — point hit-testing over scene-mm candidates.
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/AvaloniaRenderResources.cs` — bounded/profile-aware pens, brushes, text and symbol geometry caches.
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/AvaloniaSceneRenderer.cs` — immediate-mode scene renderer.
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/InteractionOverlayState.cs` — passive selected/hovered IDs only.
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/InteractionOverlayRenderer.cs` — non-scene hover/selection overlay rendering.
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/SingleLineView.cs` — Avalonia control composing renderer/viewport/index/hit-testing.
- `src/UI_Unilineal.Rendering.Avalonia/UI_Unilineal.Rendering.Avalonia.csproj` — add Engine reference only.

### Rendering tests

- `tests/UI_Unilineal.Rendering.Avalonia.Tests/UI_Unilineal.Rendering.Avalonia.Tests.csproj`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Viewport/ViewportControllerTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/SceneSpatialIndexTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/HitTesting/HitTestIndexTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/AvaloniaSceneRendererTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/SingleLineViewTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Architecture/RenderingDependencyBoundaryTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Fixtures/RenderingSceneFixtures.cs`

### Playground

- `src/UI_Unilineal.Playground/Fixtures/PlaygroundFixtureFactory.cs`
- `src/UI_Unilineal.Playground/ViewModels/SingleLineWorkspaceViewModel.cs`
- `src/UI_Unilineal.Playground/Views/MainWindow.axaml`
- `src/UI_Unilineal.Playground/Views/MainWindow.axaml.cs`

### Solution/docs

- `UI_Unilineal.sln`
- `README.md`
- `docs/architecture/README.md`
- `.github/workflows/ci.yml` only if the new test project needs an explicit platform setting; otherwise leave CI workflow unchanged.

---

### Task 25: Rendering dependency and headless-test scaffold

**Files:**
- Modify: `src/UI_Unilineal.Rendering.Avalonia/UI_Unilineal.Rendering.Avalonia.csproj`
- Create: `tests/UI_Unilineal.Rendering.Avalonia.Tests/UI_Unilineal.Rendering.Avalonia.Tests.csproj`
- Create: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Architecture/RenderingDependencyBoundaryTests.cs`
- Modify: `UI_Unilineal.sln`

**Interfaces:**
- Rendering may consume Domain and Engine.
- Test project consumes Rendering.Avalonia, Engine and Domain.
- No production type is introduced in this task.

- [ ] **Step 1: RED — add dependency-boundary tests before changing project references**

Create tests that assert:
```csharp
[Fact]
public void RenderingAvalonia_ReferencesDomainAndEngineButNotPlaygroundOrHost()
{
    string[] refs = typeof(UI_Unilineal.Rendering.Avalonia.AssemblyMarker)
        .Assembly.GetReferencedAssemblies()
        .Select(x => x.Name ?? string.Empty)
        .ToArray();

    Assert.Contains("UI_Unilineal.Domain", refs);
    Assert.Contains("UI_Unilineal.Engine", refs);
    Assert.DoesNotContain(refs, x =>
        x.StartsWith("UI_Unilineal.Playground", StringComparison.Ordinal));
    Assert.DoesNotContain(refs, x =>
        x.StartsWith("ProyectoElectrico", StringComparison.Ordinal));
}

[Fact]
public void DomainAndEngine_StillDoNotReferenceAvalonia()
{
    Assert.DoesNotContain(
        typeof(UI_Unilineal.Domain.AssemblyMarker)
            .Assembly.GetReferencedAssemblies(),
        x => (x.Name ?? "").StartsWith("Avalonia", StringComparison.Ordinal));

    Assert.DoesNotContain(
        typeof(UI_Unilineal.Engine.AssemblyMarker)
            .Assembly.GetReferencedAssemblies(),
        x => (x.Name ?? "").StartsWith("Avalonia", StringComparison.Ordinal));
}
```

- [ ] **Step 2: RED — create the test project with exact package versions**

Use:
```xml
<TargetFramework>net9.0</TargetFramework>
<PackageReference Include="Avalonia.Headless.XUnit" Version="11.3.18" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
<PackageReference Include="xunit" Version="2.9.2" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
```

Project references:
```xml
<ProjectReference Include="..\..\src\UI_Unilineal.Domain\UI_Unilineal.Domain.csproj" />
<ProjectReference Include="..\..\src\UI_Unilineal.Engine\UI_Unilineal.Engine.csproj" />
<ProjectReference Include="..\..\src\UI_Unilineal.Rendering.Avalonia\UI_Unilineal.Rendering.Avalonia.csproj" />
```

Add it to the solution under the existing `tests` solution folder.

- [ ] **Step 3: Run the new architecture test and observe RED**

Run:
```powershell
dotnet test tests\UI_Unilineal.Rendering.Avalonia.Tests\UI_Unilineal.Rendering.Avalonia.Tests.csproj -c Release --filter "FullyQualifiedName~RenderingDependencyBoundaryTests"
```

Expected: failure because Rendering.Avalonia does not yet reference Engine.

- [ ] **Step 4: GREEN — add exactly one new production dependency**

Add:
```xml
<ProjectReference Include="..\UI_Unilineal.Engine\UI_Unilineal.Engine.csproj" />
```
to Rendering.Avalonia. Do not reference Playground.

- [ ] **Step 5: Verify architecture tests and full solution**

Run restore/build/tests. Require zero warnings/errors and previous 45 Domain + 158 Engine tests unchanged/green.

- [ ] **Step 6: Commit**

```text
test(rendering): establish Avalonia renderer dependency boundary
```

---

### Task 26: Viewport state and reversible mm/DIP transform

**Files:**
- Create: `src/UI_Unilineal.Rendering.Avalonia/Viewport/ViewportState.cs`
- Create: `src/UI_Unilineal.Rendering.Avalonia/Viewport/ViewportTransform.cs`
- Create: `src/UI_Unilineal.Rendering.Avalonia/Viewport/ViewportController.cs`
- Test: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Viewport/ViewportControllerTests.cs`

**Interfaces:**
```csharp
public sealed record ViewportState(
    double Zoom,
    Vector PanDip,
    Size ViewportDip,
    double MinZoom = 0.05,
    double MaxZoom = 32.0);

public static class ViewportTransform
{
    public const double DipsPerMillimetre = 96.0 / 25.4;

    public static Point SceneMmToDip(MmPoint point, ViewportState state);
    public static MmPoint DipToSceneMm(Point point, ViewportState state);
    public static Rect SceneRectMmToDip(MmRect rect, ViewportState state);
    public static MmRect DipRectToSceneMm(Rect rect, ViewportState state);
}

public sealed class ViewportController
{
    public ViewportState ZoomAt(ViewportState state, Point cursorDip, double zoomFactor);
    public ViewportState Pan(ViewportState state, Vector deltaDip);
    public ViewportState FitScene(ViewportState state, MmRect sceneBounds, double marginDip = 24);
    public ViewportState FitSelection(ViewportState state, MmRect selectionBounds, double marginDip = 24);
    public ViewportState ActualSize(ViewportState state, MmPoint? centerSceneMm = null);
    public ViewportState CenterOn(ViewportState state, MmPoint point);
}
```

- [ ] **Step 1: RED — exact physical conversion and round-trip**

Tests assert:
```csharp
Assert.Equal(96.0 / 25.4, ViewportTransform.DipsPerMillimetre, 12);
MmPoint source = new(123.456, -17.25);
Point dip = ViewportTransform.SceneMmToDip(source, state);
MmPoint roundTrip = ViewportTransform.DipToSceneMm(dip, state);
Assert.Equal(source.X, roundTrip.X, 9);
Assert.Equal(source.Y, roundTrip.Y, 9);
```

- [ ] **Step 2: RED — ZoomAt preserves the scene point under the cursor**

Capture `before = DipToSceneMm(cursor)`, call `ZoomAt`, then assert `after == before` within 1e-9 and zoom is clamped to MinZoom/MaxZoom.

- [ ] **Step 3: RED — Pan changes only PanDip**

Assert scene data is not part of `ViewportState`; `Pan` returns `PanDip + delta` and leaves zoom/viewport limits unchanged.

- [ ] **Step 4: RED — FitScene/FitSelection/ActualSize/CenterOn**

For a 100 x 50 mm rect in a 1000 x 600 DIP viewport:
- fit respects margin;
- entire rect maps inside viewport;
- aspect ratio is preserved;
- `ActualSize` sets zoom = 1.0;
- `CenterOn` maps requested scene point to viewport center.

- [ ] **Step 5: GREEN**

Implement transforms using:
```csharp
double scale = ViewportTransform.DipsPerMillimetre * state.Zoom;
dipX = sceneX * scale + state.PanDip.X;
dipY = sceneY * scale + state.PanDip.Y;
```
and the exact inverse.

- [ ] **Step 6: Run focused + full tests and commit**

```text
feat(rendering): add deterministic millimetre viewport
```

---

### Task 27: Scene spatial index and viewport culling

**Files:**
- Create: `src/UI_Unilineal.Rendering.Avalonia/Rendering/SceneSpatialIndex.cs`
- Create: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/SceneSpatialIndexTests.cs`
- Create: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Fixtures/RenderingSceneFixtures.cs`

**Interfaces:**
```csharp
public sealed class SceneSpatialIndex
{
    public SceneSpatialIndex(DiagramScene scene, double cellSizeMm = 50.0);
    public DiagramScene Scene { get; }
    public IReadOnlyList<SceneElement> Query(MmRect viewportMm);
    public IReadOnlyList<SceneElement> QueryPoint(MmPoint point, double toleranceMm);
}
```

The index stores immutable element references in deterministic uniform-grid buckets. It does not store Avalonia controls or DIPs.

- [ ] **Step 1: RED — cell boundary query**

Create three elements in separate 50 mm cells. Query a rect crossing two cells and assert only intersecting elements are returned.

- [ ] **Step 2: RED — multi-cell element is deduplicated**

An element spanning four cells must appear once in query results.

- [ ] **Step 3: RED — equivalent shuffled scene produces identical query ordering**

Expected ordering function:
```csharp
OrderBy(e => e.Layer)
.ThenBy(e => e.ZIndex)
.ThenBy(e => e.Id.Value, StringComparer.Ordinal)
```

- [ ] **Step 4: RED — large-scene smoke**

Build at least 5000 simple elements on a regular grid, index them, query a small viewport, assert:
- no exception;
- every returned element intersects viewport;
- result count is much smaller than total scene count.

No absolute timing gate is introduced yet; G9 owns calibrated performance thresholds.

- [ ] **Step 5: GREEN uniform-grid index**

Use floor division by `cellSizeMm`; guard against non-finite/non-positive cell size. Query must deduplicate by `SceneId`.

- [ ] **Step 6: Commit**

```text
feat(rendering): index scene geometry for deterministic culling
```

---

### Task 28: Deterministic hit-testing in scene millimetres

**Files:**
- Create: `src/UI_Unilineal.Rendering.Avalonia/HitTesting/HitTestPolicy.cs`
- Create: `src/UI_Unilineal.Rendering.Avalonia/HitTesting/HitTestResult.cs`
- Create: `src/UI_Unilineal.Rendering.Avalonia/HitTesting/HitTestIndex.cs`
- Test: `tests/UI_Unilineal.Rendering.Avalonia.Tests/HitTesting/HitTestIndexTests.cs`

**Interfaces:**
```csharp
public sealed record HitTestPolicy(
    double ToleranceDip = 6.0,
    double AnchorRadiusDip = 8.0);

public enum HitKind
{
    Anchor,
    Symbol,
    SemanticText,
    Branch,
    Connection,
    Group,
    Background
}

public sealed record HitTestResult(
    SceneId SceneElementId,
    EntityReference? Entity,
    HitKind Kind,
    string? AnchorId,
    double DistanceMm,
    int Priority);

public sealed class HitTestIndex
{
    public HitTestIndex(DiagramScene scene, SceneSpatialIndex spatialIndex);
    public IReadOnlyList<HitTestResult> HitTest(
        Point pointerDip,
        ViewportState viewport,
        HitTestPolicy policy);
}
```

- [ ] **Step 1: RED — DIP tolerance converts to scene millimetres**

At zoom 1 and zoom 4, the same 6 DIP tolerance must cover different mm distances by inverse scale; no hard-coded mm hit radius.

- [ ] **Step 2: RED — anchor outranks owning symbol/group**

Pointer inside both an anchor radius and group bounds must return Anchor first.

- [ ] **Step 3: RED — deterministic priority matrix**

Required priority:
```text
Anchor > Symbol > SemanticText > Branch > Connection > Group > Background
```
Ties use smaller `DistanceMm`, then `SceneId`, then AnchorId.

- [ ] **Step 4: RED — line/polyline tolerance**

A thin route can be hit within tolerance even though its visual line width is smaller. Use point-to-segment distance, not bounds-only hit testing.

- [ ] **Step 5: RED — ambiguous hits are returned in stable order**

Return all candidates sorted by the policy; do not let dictionary/hash iteration choose the primary result.

- [ ] **Step 6: GREEN and commit**

```text
feat(rendering): add deterministic scene hit testing
```

---

### Task 29: Avalonia drawing resources and resolved styles

**Files:**
- Create: `src/UI_Unilineal.Rendering.Avalonia/Rendering/AvaloniaRenderResources.cs`
- Create: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/AvaloniaRenderResourcesTests.cs`

**Interfaces:**
```csharp
public enum InteractiveThemeKind
{
    Light,
    Dark
}

public sealed class AvaloniaRenderResources
{
    public AvaloniaRenderResources(
        RIC18DrawingProfile profile,
        InteractiveThemeKind theme,
        int maxCachedSymbols = 256,
        int maxCachedTextEntries = 2048);

    public Pen ResolvePen(string lineStyleId);
    public Typeface ResolveTypeface(string textStyleId);
    public IBrush ResolveTextBrush(string textStyleId);
    public IBrush ResolveStatusBrush(string status);
    public Geometry ResolveSymbolGeometry(
        string symbolDefinitionId,
        string variant = "default");

    public string ProfileFingerprint { get; }
}
```

G6 renderer uses profile semantics, but status must not be encoded only by colour: warning/error/selected overlays also use dash/thickness/outline semantics in the renderer.

- [ ] **Step 1: RED — missing style/symbol throws typed `KeyNotFoundException`**

No silent default for unknown technical profile IDs.

- [ ] **Step 2: RED — cache key includes profile fingerprint + symbol ID + variant**

Same key returns the same cached geometry instance; distinct variant or distinct profile fingerprint does not alias.

- [ ] **Step 3: RED — cache is bounded**

After inserting more than configured capacity, cache count never exceeds capacity. Deterministic FIFO/LRU policy is acceptable; choose FIFO for simpler reproducibility.

- [ ] **Step 4: GREEN — convert neutral profile primitives to Avalonia geometry/resources**

No `DiagramScene` mutation and no layout calculation.

- [ ] **Step 5: Commit**

```text
feat(rendering): resolve profile styles into bounded Avalonia resources
```

---

### Task 30: Immediate-mode Avalonia scene renderer

**Files:**
- Create: `src/UI_Unilineal.Rendering.Avalonia/Rendering/AvaloniaSceneRenderer.cs`
- Create: `src/UI_Unilineal.Rendering.Avalonia/Rendering/InteractionOverlayState.cs`
- Create: `src/UI_Unilineal.Rendering.Avalonia/Rendering/InteractionOverlayRenderer.cs`
- Test: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/AvaloniaSceneRendererTests.cs`

**Interfaces:**
```csharp
public sealed record InteractionOverlayState(
    SceneId? Hovered,
    IReadOnlySet<SceneId> Selected);

public sealed class AvaloniaSceneRenderer
{
    public void Render(
        DrawingContext context,
        DiagramScene scene,
        RIC18DrawingProfile profile,
        ViewportState viewport,
        SceneSpatialIndex index,
        AvaloniaRenderResources resources);
}
```

- [ ] **Step 1: RED — deterministic visible render sequence**

Extract a testable `BuildRenderList(viewport)` helper or internal method. Assert order:
```text
Layer -> ZIndex -> SceneId
```
and that off-viewport elements are excluded.

- [ ] **Step 2: RED — every G5 primitive kind has a render path**

Fixtures cover Line, Polyline, Rectangle, Circle, Path, Text, Symbol and Group. Group itself may be non-painted if it is only a semantic container, but it must not create a child Avalonia control.

- [ ] **Step 3: RED — rendering does not mutate scene fingerprint**

Compute `DiagramSceneFingerprint` before and after headless render; fingerprints must match.

- [ ] **Step 4: RED — interaction overlays are not scene elements**

Render selected/hovered overlays and assert scene element count/fingerprint are unchanged.

- [ ] **Step 5: GREEN immediate renderer**

Use one `DrawingContext`; apply viewport transform at draw time. Route elements already exist as scene polylines and are rendered exactly once.

- [ ] **Step 6: Headless smoke**

Render a minimal summary and nested board detail to an Avalonia headless surface/control without exception.

- [ ] **Step 7: Commit**

```text
feat(rendering): draw neutral scenes with immediate Avalonia renderer
```

---

### Task 31: `SingleLineView` control and viewport input

**Files:**
- Create: `src/UI_Unilineal.Rendering.Avalonia/Rendering/SingleLineView.cs`
- Test: `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/SingleLineViewTests.cs`

**Interfaces:**
```csharp
public sealed class SingleLineView : Control
{
    public DiagramScene? Scene { get; set; }
    public RIC18DrawingProfile? DrawingProfile { get; set; }

    public ViewportState Viewport { get; }
    public InteractionOverlayState Overlay { get; }

    public void FitScene();
    public void FitSelection();
    public void ZoomIn();
    public void ZoomOut();
    public void ActualSize();
    public void CenterOn(MmPoint point);

    public event EventHandler<HitTestResult?>? PrimaryHitChanged;
}
```

The public API may use Avalonia styled/direct properties for `Scene` and `DrawingProfile`, but the semantic types remain Domain/Engine types.

- [ ] **Step 1: RED — scene replacement is atomic**

Set scene A, then scene B. After replacement:
- one spatial index corresponds only to B;
- no element from A can be hit;
- viewport object remains valid;
- no mutation occurs to either scene.

- [ ] **Step 2: RED — wheel zoom preserves cursor scene point**

Synthesize wheel/pointer input or call the internal input adapter through a headless test; verify ZoomAt invariant.

- [ ] **Step 3: RED — pan gestures alter viewport only**

Middle-button drag and Space+left drag update `PanDip`; scene fingerprint stays unchanged.

- [ ] **Step 4: RED — keyboard viewport commands**

Required G6 commands:
- `+` -> ZoomIn
- `-` -> ZoomOut
- `F` -> FitScene
- `Shift+F` -> FitSelection when a selection exists, otherwise no crash
- `Home` -> FitScene/center stable default

G7 owns Delete/Escape command semantics beyond cancelling view-only gestures.

- [ ] **Step 5: RED — pointer hit updates passive hover/primary hit**

The control may expose hit events but must not navigate, edit topology or execute commands.

- [ ] **Step 6: GREEN**

`Render(DrawingContext)` delegates to `AvaloniaSceneRenderer`; no child controls are created per scene element.

- [ ] **Step 7: Commit**

```text
feat(rendering): add single-line immediate-mode Avalonia view
```

---

### Task 32: Playground summary/detail workflow

**Files:**
- Create: `src/UI_Unilineal.Playground/Fixtures/PlaygroundFixtureFactory.cs`
- Create: `src/UI_Unilineal.Playground/ViewModels/SingleLineWorkspaceViewModel.cs`
- Modify: `src/UI_Unilineal.Playground/Views/MainWindow.axaml`
- Modify: `src/UI_Unilineal.Playground/Views/MainWindow.axaml.cs`

**Interfaces:**
```csharp
public enum PlaygroundRouteKind
{
    ProjectSummary,
    BoardDetail
}

public sealed record PlaygroundRoute(
    PlaygroundRouteKind Kind,
    EntityUid? BoardUid = null);

public sealed class SingleLineWorkspaceViewModel
{
    public DiagramScene Scene { get; }
    public RIC18DrawingProfile Profile { get; }
    public PlaygroundRoute CurrentRoute { get; }
    public bool CanGoBack { get; }

    public void ShowSummary();
    public void OpenBoard(EntityUid boardUid);
    public void Back();
}
```

- [ ] **Step 1: RED — route/navigation model**

Tests or pure assertions in a small Playground-independent helper prove:
```text
Summary -> Board B1 -> Board B2 -> Back -> B1 -> Back -> Summary
```
The navigation stack lives in the Playground shell, never in `SingleLineView`.

- [ ] **Step 2: GREEN — fixture**

Use a deterministic nested-board fixture equivalent to Engine's canonical nested case. Project it once and produce scenes through:
```csharp
new SingleLineLayoutEngine(new DeterministicTextMetrics())
```
with repository `data/ric18/v1` profile.

- [ ] **Step 3: Build the shell without a large code-behind**

`MainWindow.axaml` contains:
- toolbar: Back, Summary, Zoom -, Zoom +, Fit;
- compact left navigation/inspector panel with B1/B2 buttons and current route text;
- dedicated main `SingleLineView`.

`MainWindow.axaml.cs` may wire commands/events and DataContext, but must not contain projection/layout algorithms.

- [ ] **Step 4: Manual-runtime acceptance contract**

A developer can launch:
```powershell
dotnet run --project src\UI_Unilineal.Playground\UI_Unilineal.Playground.csproj -c Release
```
and verify:
- Summary is visible;
- B1 detail opens;
- B2 detail opens;
- Back restores prior route;
- zoom +/- and Fit work;
- resizing does not change scene fingerprint.

This is a local acceptance step, not a substitute for automated tests.

- [ ] **Step 5: Commit**

```text
feat(playground): expose summary and board-detail renderer workflow
```

---

### Task 33: G6 hardening, review and checkpoint

**Files:**
- Modify: `README.md`
- Modify: `docs/architecture/README.md`
- Test: existing/new G6 tests only; no new production feature unless self-review finds a defect.

**Interfaces:** none new.

- [ ] **Step 1: Full G6 invariant run**

Run:
```powershell
dotnet restore UI_Unilineal.sln
dotnet format UI_Unilineal.sln --verify-no-changes --no-restore
dotnet build UI_Unilineal.sln -c Release --no-restore
dotnet test UI_Unilineal.sln -c Release --no-build
git diff --check
git status --porcelain
```

Require:
- zero warnings/errors;
- all Domain, Engine and Rendering.Avalonia tests green;
- empty `git diff --check`;
- empty working tree after checkpoint commit.

- [ ] **Step 2: Architecture gate**

Explicitly re-run:
```powershell
dotnet test tests\UI_Unilineal.Engine.Tests\UI_Unilineal.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~DependencyBoundaryTests"
dotnet test tests\UI_Unilineal.Rendering.Avalonia.Tests\UI_Unilineal.Rendering.Avalonia.Tests.csproj -c Release --filter "FullyQualifiedName~RenderingDependencyBoundaryTests"
```

- [ ] **Step 3: Renderer stress smoke**

Use a G5-generated large scene and assert:
- spatial query returns only visible candidates;
- headless render completes;
- hit-testing completes and returns stable ordering;
- scene fingerprint before/after render is identical.

Do not introduce arbitrary millisecond pass/fail budgets; G9 will establish calibrated benchmark baselines.

- [ ] **Step 4: Self-review against spec §§14 and 16**

Check and fix with regression tests before proceeding:
- one immediate-mode control, not per-element controls;
- viewport operations do not mutate scene;
- culling/index operates in scene mm;
- hit tolerance is distinct from stroke width;
- deterministic render/hit ordering;
- overlays are not persisted into scene;
- shell, not renderer, owns navigation;
- no electrical/layout command state machine has leaked into G6;
- no exporter/document code has leaked into G6.

- [ ] **Step 5: Documentation**

Update README/architecture with:
```text
G6 implemented:
DiagramScene -> SceneSpatialIndex -> Viewport -> AvaloniaSceneRenderer -> SingleLineView
```
and state explicitly that G7 owns interaction modes/commands and G8 owns document/export.

- [ ] **Step 6: Commit checkpoint**

```text
docs: mark V1 G6 Avalonia renderer checkpoint
```

- [ ] **Step 7: Verify CI on exact final SHA**

Require both Windows and Ubuntu jobs GREEN including repository-cleanliness step.

- [ ] **Step 8: Optional checkpoint tag**

Desired tag:
```text
g6-green-2026-09-18
```
If the active GitHub connector still cannot create tag refs, record the exact SHA and create the tag locally later rather than moving the branch or inventing a tag state.

---

## Plan self-review

### Spec coverage

- §14 immediate-mode `SingleLineView`: Tasks 30–31.
- §14.1 viewport transforms and operations: Task 26 + Task 31 input bindings.
- §14.2 culling/cache/order: Tasks 27, 29, 30.
- §14.3 hit testing: Task 28.
- §14.4 passive overlays: Task 30; the full interaction state machine is intentionally deferred to G7.
- §16 summary/detail/back navigation outside renderer: Task 32.
- Architecture/dependency invariants: Tasks 25 and 33.
- Large-scene smoke without premature hard performance threshold: Tasks 27 and 33.

### Explicit G6 exclusions

G6 does not implement `Navigate/Layout/Electrical` modes, command proposals, host command execution, layout undo/redo, electrical undo/redo, SVG, PDF, sheet composition, or ProyectoElectrico integration.

### Placeholder scan

No TBD/TODO/"similar to" steps are present. Every production type introduced by the plan has an owning task and a consumer/test.

### Type consistency

- `ViewportState` and `ViewportTransform` are defined in Task 26 and consumed unchanged by Tasks 28, 30 and 31.
- `SceneSpatialIndex` is defined in Task 27 and consumed by Tasks 28, 30 and 31.
- `HitTestResult` is defined in Task 28 and exposed by `SingleLineView` in Task 31.
- `AvaloniaRenderResources` is defined in Task 29 and consumed by Task 30.
- `InteractionOverlayState` is passive renderer state only; G7 may later compose a richer interaction controller around it without changing `DiagramScene`.
- Playground route types remain in Playground and do not cross into Rendering.Avalonia.

### Sequencing rationale

Tasks 26–28 establish pure geometry/selection infrastructure before rendering; Task 29 establishes bounded rendering resources; Task 30 adds drawing; Task 31 adds the control/input shell; Task 32 validates the user-facing summary/detail workflow. This prevents `SingleLineView` from becoming a monolithic controller.
