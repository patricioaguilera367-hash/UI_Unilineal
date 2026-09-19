# UI_Unilineal V1 G8 — Documents and Exports Implementation Plan

> **Execution rule:** implement task-by-task with RED -> observed RED -> minimal GREEN -> focused tests -> full regression -> commit. Every task must preserve all G0-G7 evidence.

**Goal:** Add physical document composition and deterministic vector export over the closed G7 scene pipeline. G8 turns immutable `DiagramScene` values into one or more physical `DrawingSheet` objects, then serializes the same `DrawingDocument` semantics to SVG and PDF without screen capture, raster fallback, Avalonia dependencies or ProyectoElectrico integration.

**Architecture:** `UI_Unilineal.Engine` owns renderer-neutral document composition, pagination, title-block semantics, preflight and export traceability. `UI_Unilineal.Export.Svg` and `UI_Unilineal.Export.Pdf` depend on Engine/Domain and serialize a composed document to streams. Exporters never calculate electrical semantics, projection or layout. Playground wiring is acceptance-only and capability-gated.

**Spec:** `docs/superpowers/specs/2026-09-16-single-line-v1-design.md` §18, §20.2, §20.4, §20.5, §23 and G8 in §24.

## Global constraints

- Preserve dependency direction: exporters -> Engine -> Domain.
- Domain and Engine remain free of Avalonia, filesystem persistence, PDF/SVG backend details and ProyectoElectrico.
- `DiagramScene` remains logical drawing geometry in millimetres.
- `DrawingDocument` is physical composition: sheets, paper, margins, scale, view box, title block and scene viewport.
- Screen zoom is unrelated to document scale.
- Low-level exporters write to `Stream` and accept cancellation.
- SVG must be deterministic, self-contained, script-free and safely escape project text.
- PDF must be vector-first and multipage; no screenshot or silent raster fallback.
- Both exporters consume the same `DrawingDocument`.
- Export preflight runs before bytes are written.
- `ExportManifest` separates semantic fingerprints from binary hashes/timestamps.
- Exact font availability is validated in strict mode; exporters may not silently substitute a layout-changing font.
- G8 does not persist project data or integrate ProyectoElectrico (G10/G11).
- Final G8 checkpoint requires Windows and Ubuntu CI GREEN on the exact final SHA, zero warnings/errors, format clean and repository cleanliness green.

---

## File map locked for G8

### Engine document contracts/composition

- `src/UI_Unilineal.Engine/Documents/PaperSize.cs`
- `src/UI_Unilineal.Engine/Documents/DrawingDocument.cs`
- `src/UI_Unilineal.Engine/Documents/DrawingSheet.cs`
- `src/UI_Unilineal.Engine/Documents/TitleBlock.cs`
- `src/UI_Unilineal.Engine/Documents/DocumentCompositionPolicy.cs`
- `src/UI_Unilineal.Engine/Documents/DocumentComposer.cs`
- `src/UI_Unilineal.Engine/Documents/DocumentPreflight.cs`
- `src/UI_Unilineal.Engine/Documents/ExportManifest.cs`
- `src/UI_Unilineal.Engine/Documents/ResolvedDrawingStyle.cs`
- `src/UI_Unilineal.Engine/Documents/PrintStyleResolver.cs`

### SVG exporter

- `src/UI_Unilineal.Export.Svg/UI_Unilineal.Export.Svg.csproj`
- `src/UI_Unilineal.Export.Svg/SvgExporter.cs`
- `src/UI_Unilineal.Export.Svg/SvgExportOptions.cs`

### PDF exporter

- `src/UI_Unilineal.Export.Pdf/UI_Unilineal.Export.Pdf.csproj`
- `src/UI_Unilineal.Export.Pdf/PdfExporter.cs`
- `src/UI_Unilineal.Export.Pdf/PdfExportOptions.cs`

### Tests

- `tests/UI_Unilineal.Engine.Tests/Documents/**`
- `tests/UI_Unilineal.Export.Svg.Tests/**`
- `tests/UI_Unilineal.Export.Pdf.Tests/**`
- `tests/**/Architecture/**`
- `tests/**/Golden/**`

### Playground/docs

- `src/UI_Unilineal.Playground/ViewModels/SingleLineWorkspaceViewModel.cs`
- `src/UI_Unilineal.Playground/Views/MainWindow.axaml`
- `README.md`
- `docs/architecture/README.md`

---

### Task 42: Physical document contracts and paper presets

**Purpose:** establish renderer-neutral physical-document vocabulary before pagination or serialization.

Required concepts:

- `PaperPreset`: A0, A1, A2, A3, A4, Custom.
- `PageOrientation`: Portrait, Landscape.
- `PaperSize` with exact millimetre dimensions and deterministic orientation transform.
- `SheetMargins` in millimetres.
- `TitleBlock` with project/sheet/document fields as data, not exporter text hacks.
- `DrawingSheet`: sheet number, physical size, scale, view box, scene viewport, margins, title block and scene.
- `DrawingDocument`: stable document ID/revision plus ordered immutable sheets.

Rules:

- A-series dimensions are explicit constants in mm.
- All geometry must be finite and positive.
- Sheet numbers are one-based and contiguous.
- Physical paper size and scene/view boxes cannot be inferred from screen viewport state.

- [ ] RED: paper preset dimensions/orientation matrix.
- [ ] RED: invalid dimensions/margins/scale/view boxes reject construction.
- [ ] RED: document rejects duplicate/non-contiguous sheet numbering.
- [ ] GREEN: contracts only; no pagination/export yet.
- [ ] Commit: `feat(documents): define physical drawing document contracts`.

---

### Task 43: Document composer, fit policy and deterministic continuation

**Purpose:** transform one or more `DiagramScene` values into physical sheets without modifying the scenes.

Policy includes:

- preferred paper preset/orientation;
- allowed fallback paper sizes;
- margins/title-block reserved area;
- preferred drawing scale;
- minimum legibility scale;
- one-board-per-sheet preference;
- deterministic continuation policy.

Rules:

- fit-to-sheet may not reduce below the configured minimum legibility threshold;
- if preferred paper cannot fit, choose the first configured larger acceptable sheet;
- if no acceptable single sheet fits, create deterministic continuation sheets;
- continuation sheets expose explicit continuation metadata/references;
- composition is deterministic for the same scene + policy;
- source scene fingerprint remains unchanged.

- [ ] RED: simple scene fits preferred sheet at preferred scale.
- [ ] RED: fallback to larger sheet occurs before sub-legibility scaling.
- [ ] RED: oversized detail paginates deterministically.
- [ ] RED: equivalent scene order produces equivalent document composition.
- [ ] GREEN: pure `DocumentComposer`.
- [ ] Commit: `feat(documents): compose deterministic physical sheets`.

---

### Task 44: Shared print style resolution, preflight and export manifest

**Purpose:** centralize export validity and traceability so SVG/PDF do not invent independent rules.

Preflight validates at least:

- non-empty document and contiguous sheets;
- finite/positive geometry;
- scene viewport inside printable area;
- known line/text style IDs;
- required font declarations;
- strict-font availability supplied by host/export options;
- no unresolved exporter-incompatible primitive.

`ExportManifest` records:

- ProjectUid;
- input fingerprint;
- projection fingerprint;
- scene fingerprint(s);
- drawing profile ID/version/fingerprint;
- layout engine version;
- exporter ID/version;
- document revision;
- sheet count.

- [ ] RED: invalid style/font/geometry blocks export before stream mutation.
- [ ] RED: manifest is deterministic for same semantic document/exporter version.
- [ ] RED: semantic manifest excludes timestamps and output file hashes.
- [ ] GREEN: resolver + preflight + manifest contracts.
- [ ] Commit: `feat(documents): add export preflight styles and traceability`.

---

### Task 45: Deterministic self-contained SVG exporter

**Purpose:** provide the reference vector exporter and regression surface.

Rules:

- consumes `DrawingDocument`, never `DiagramScene` directly;
- serializes physical dimensions in mm and deterministic viewBox values;
- emits line/polyline/rect/circle/path/text as vector/text;
- preserves useful semantic grouping/IDs without executable content;
- no scripts, external CSS, remote fonts or remote images;
- escapes XML/project text safely;
- deterministic ordering and invariant-culture numbers;
- one SVG document per selected sheet; multipage export API returns ordered named entries rather than inventing nonstandard multipage SVG;
- cancellation is observed.

- [ ] RED: minimal sheet SVG structural golden.
- [ ] RED: hostile text is escaped and cannot inject script/style/image references.
- [ ] RED: deterministic bytes for equivalent document.
- [ ] RED: all supported scene primitives remain vector/text.
- [ ] GREEN: exporter.
- [ ] Commit: `feat(export-svg): add deterministic vector SVG exporter`.

---

### Task 46: Vector-first multipage PDF exporter

**Purpose:** serialize the same document semantics into a valid physical multipage PDF without rasterization.

Implementation constraint: use a deterministic minimal PDF writer or a backend proven by tests to preserve required vector primitives/text. Do not introduce screen capture.

Required behavior:

- physical MediaBox per sheet;
- one page per `DrawingSheet`;
- line/polyline/rectangle/circle/path/text represented by PDF vector/text operators;
- clipping/view transform preserves document scale;
- line widths and dash patterns are represented;
- output contains no raster image objects for supported primitives;
- cancellation is observed.

- [ ] RED: PDF header/xref/trailer structural validity.
- [ ] RED: page count and MediaBox match document sheets.
- [ ] RED: content streams contain vector/text operators and no image XObjects.
- [ ] RED: same semantic document is deterministic.
- [ ] GREEN: vector PDF exporter.
- [ ] Commit: `feat(export-pdf): add deterministic vector multipage PDF exporter`.

---

### Task 47: Export parity and document regression evidence

**Purpose:** prove SVG and PDF serialize the same physical-document semantics.

- [ ] Contract test: both exporters consume the exact same `DrawingDocument` and manifest source.
- [ ] Golden documents for minimal summary, minimal detail and nested detail.
- [ ] Verify sheet count, physical size, view box and scene fingerprints agree across SVG/PDF evidence.
- [ ] Verify shuffled equivalent semantic input still yields equivalent composed/exported structure.
- [ ] Verify exporters never reference Avalonia or ProyectoElectrico.
- [ ] Commit: `test(exports): add cross-format G8 regression evidence`.

---

### Task 48: Playground export orchestration and atomic host-boundary write helper

**Purpose:** prove G8 can be invoked from the shell while low-level exporters stay stream-only.

Rules:

- capability gating uses `HostCapabilities.CanExport`;
- shell composes/preflights before opening/replacing destination;
- host-boundary helper writes a temporary file and atomically replaces destination only after successful completion;
- cancellation/failure leaves existing destination untouched;
- Playground may expose deterministic demo export actions but does not become canonical persistence.

- [ ] RED: capability false disables export orchestration.
- [ ] RED: preflight failure leaves destination untouched.
- [ ] RED: cancelled/failed write leaves prior file intact.
- [ ] GREEN: shell/helper integration.
- [ ] Commit: `feat(playground): wire capability-gated document export`.

---

### Task 49: G8 hardening

- [ ] Malicious/unusual Unicode/XML/PDF text regression cases.
- [ ] Boundary paper sizes, zero/near-zero invalid geometry and large sheets.
- [ ] Cancellation tests for SVG/PDF stream writers.
- [ ] Architecture guards against Avalonia/ProyectoElectrico leakage.
- [ ] Cross-platform invariant-culture deterministic export checks where feasible.
- [ ] Full restore/format/build/test/diff/cleanliness run.
- [ ] Commit: `test(exports): harden G8 document and vector export pipeline`.

---

### Task 50: G8 self-review and checkpoint

- [ ] Self-review against design §18, export invariants in §20.2/20.5 and V1 flow in §23.
- [ ] Confirm no screen capture/raster fallback paths exist.
- [ ] Confirm exporter projects are downstream adapters only.
- [ ] Update solution, README and architecture docs.
- [ ] Record known limitation: visual/manual inspection remains complementary to headless structural evidence.
- [ ] Commit: `docs: mark V1 G8 document export checkpoint`.
- [ ] Verify exact final SHA CI: Windows GREEN + Ubuntu GREEN.
- [ ] Optional tag desired: `g8-green-2026-09-19`.

---

## G8 acceptance boundary

G8 is GREEN only when all of the following are simultaneously true:

1. `DrawingDocument`/`DrawingSheet` model physical sheets independently from screen state.
2. Composer respects preferred scale, minimum legibility and deterministic fallback/continuation.
3. Title blocks and continuation references are document semantics, not exporter-specific text hacks.
4. Preflight blocks invalid output before writing.
5. SVG is deterministic, self-contained, escaped and vector/text only.
6. PDF is deterministic, multipage, physical-size correct and vector/text only for supported primitives.
7. Both exporters serialize the same composed document semantics.
8. Export traceability records semantic fingerprints/profile/layout/export versions without timestamp noise.
9. Low-level exporters write only to streams and observe cancellation.
10. G0-G7 evidence remains green and no ProyectoElectrico integration enters the gate.

## Sequencing rationale

Tasks 42-44 establish the physical-document and validity contracts before any file format appears. Task 45 makes SVG the deterministic reference surface. Task 46 adds PDF only after shared document semantics are stable. Tasks 47-49 prove parity, shell composition and robustness. Task 50 closes the gate only after the exact final SHA is green on both CI runners.
