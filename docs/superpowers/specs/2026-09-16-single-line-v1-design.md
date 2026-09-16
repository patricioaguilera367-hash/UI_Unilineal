# UI_Unilineal V1 — Architecture Design

Date: 2026-09-16
Status: Approved design, implementation not started
Branch: `feature/v1-single-line-engine`
Baseline: `main` at `16f84591a5283bb2c314749b81f29040e8174f7f`
Evidence branch: `spike/v0.4-dedicated-detail` is reference material only and is not a production base.

## 1. Purpose

V1 is the first production-grade single-line diagram subsystem. It is not a polished continuation of the V0.x spike and it is not a general-purpose CAD system. Its purpose is to provide a stable, deterministic, testable and extensible technical drawing engine that can represent an electrical project's topology, board details, protections, conductors, grounding and relevant calculation state; support safe interaction; export vector documents; and integrate with `ProyectoElectrico` without becoming a second source of electrical truth.

The production implementation starts from `main`. The V0.1–V0.4 branches remain evidence of validated UX hypotheses only: hybrid summary/detail navigation, a compact inspector, a dedicated board-detail canvas, zoom/fit/pan, downstream-board navigation, and the distinction between layout and electrical commands.

## 2. Goals

V1 shall:

- represent project-summary and per-board single-line diagrams from an immutable semantic input;
- model board supplies explicitly rather than infer electrical topology from visual hierarchy;
- keep electrical semantics, projection, composition, layout, rendering and host integration in separate layers;
- use deterministic stable identities and deterministic ordering;
- support incomplete projects without inventing missing electrical information;
- provide automatic and manually-adjustable layout with orthogonal routing;
- render interactively in Avalonia without requiring one Avalonia control per scene primitive;
- support safe selection, navigation, layout editing and explicitly-confirmed electrical command proposals;
- support layout undo/redo and inverse-command electrical undo where the host can validate it;
- export the same vector scene to SVG and PDF without screen capture or silent raster fallback;
- distinguish verified RIC-derived graphic rules from application conventions;
- integrate with `ProyectoElectrico` through a host-side anti-corruption adapter and Application-level command handling;
- preserve compatibility with legacy projects and never mutate them merely by opening or viewing a diagram;
- provide automated evidence through unit, property-based, model-based, contract, golden, integration, E2E, architecture and performance tests.

## 3. Non-goals for V1.0

V1.0 shall not implement DWG, DXF, AutoCAD synchronization, SEC electronic submission, full free-form CAD editing, 3D, simultaneous multi-user editing, cloud synchronization, cubicación, project-wide price optimization, calculation-memory generation, AI-inferred topology, automatic inference of unknown feeder circuits, or a universal catalog of every electrical symbol.

DXF, additional symbol families, multiple advanced bus systems, transfer-switch detail, transformer detail, generator detail, advanced grounding, batch editing and custom symbol libraries are architectural extension points for later 1.x work, not hidden V1.0 requirements.

## 4. Architectural principles and non-negotiable invariants

1. `ProyectoElectrico` is the canonical electrical source of truth when integrated.
2. `UI_Unilineal` never reads or writes `.peproj`, CSV or `ProyectoElectrico` domain types directly.
3. `SingleLineInput` is an immutable snapshot, not a mutable project model.
4. `SingleLineProjection` is regenerated from input and is not persisted as canonical data.
5. `DiagramScene` is the first layer that knows geometry.
6. One scene unit equals one millimetre of drawing space.
7. Renderers do not calculate layout.
8. Moving geometry never changes electrical topology.
9. Electrical topology changes are explicit commands executed and validated by the host.
10. Missing is not zero. `null` and explicit state values represent missing information.
11. The engine is strict about referential/structural integrity and tolerant about incomplete electrical data.
12. Every output must be deterministic for equivalent semantic inputs, profiles and layout state.
13. All persistent manual layout data is separate from electrical topology.
14. Exact RIC claims require explicit provenance. Application conventions must not be presented as normative requirements.
15. Viewing a project is read-only: no write-on-open or write-on-render side effects.

## 5. Dependency architecture

The core dependency direction is:

```text
UI_Unilineal.Domain
        ↑
UI_Unilineal.Engine
        ↑
├─ UI_Unilineal.Rendering.Avalonia
├─ UI_Unilineal.Export.Svg
├─ UI_Unilineal.Export.Pdf
└─ UI_Unilineal.Playground
```

`Domain` must not reference Avalonia, persistence, CSV, `.peproj` or `ProyectoElectrico`. `Engine` may reference `Domain` only. Renderers and exporters consume neutral engine/domain contracts.

The future host integration direction is:

```text
ProyectoElectrico.Application
          ↑
ProyectoElectrico.V6.Integration.SingleLine
          ├─ UI_Unilineal.Domain
          └─ UI_Unilineal.Engine

ProyectoElectrico.Desktop
          ├─ Integration.SingleLine
          └─ UI_Unilineal.Rendering.Avalonia
```

`UI_Unilineal` never references `ProyectoElectrico`.

## 6. Semantic input contract

### 6.1 `EntityUid` and `EntityReference`

`EntityUid` is an opaque stable string identity. It may wrap external GUID-backed identities and deterministic synthetic identities. It is deliberately not restricted to `Guid`, because some diagram-relevant entities do not yet have canonical host entities.

Examples of deterministic synthetic identities include:

```text
PROTECTION:<circuit-uid>:BREAKER:ADOPTED
BUS:<board-uid>:MAIN
GROUNDING:<project-uid>:MAIN
SOURCE:<project-uid>:LEGACY_MAIN
```

`EntityReference` contains `EntityUid` and `EntityKind`. Initial kinds are Project, Source, Board, Circuit, SupplyConnection, Protection, Bus, Grounding and Load. Identity validation rejects empty identities and duplicate identities in the same semantic namespace.

### 6.2 `SingleLineInput`

`SingleLineInput` is immutable and contains:

```text
Project
Sources[]
Boards[]
Buses[]
Circuits[]
SupplyConnections[]
Protections[]
Grounding[]
Results[]
Metadata
```

It contains only information required by the single-line subsystem. It is not a copy of the entire host project.

### 6.3 Project and source

`ProjectInput` contains UID, code, name and operational state.

`SourceInput` contains UID, code, name, source kind, system, nominal voltage, phase information, neutral availability, operational state and data state where applicable. Initial source kinds may include Utility, Transformer, Generator, Battery, UPS, Alternative and Unknown. Support in the semantic contract does not require V1.0 to provide a detailed symbol for every source kind.

### 6.4 Boards

`BoardInput` contains UID, code, name, role, location, nominal voltage, phase count, operational state and data state.

`BoardInput` does not contain `ParentBoardUid`. Electrical topology belongs to `SupplyConnection`.

### 6.5 Circuits and conductors

`CircuitInput` contains UID, owning BoardUid, code, name, service, electrical system, voltage, power factor, length, raceway, installation method, conductor information, operational state and data state.

`ConductorInput` carries material, type, phase section, neutral section, active-conductor count and optional descriptive information.

Protections are not embedded in `CircuitInput`.

### 6.6 Explicit supply topology

`SupplyConnection` is the canonical single-line topology relation. It contains UID, origin reference, optional through-circuit reference, destination board, role, priority, normally-active state and data/operational state.

The contract supports both:

```text
Source → SupplyConnection → Board
```

and:

```text
Board → Circuit → SupplyConnection → Downstream Board
```

A destination board may have more than one supply connection. Multiple supplies are represented semantically from V1 even if advanced transfer-system editing is introduced later.

The supply graph must reject self-supply and cycles in active structural topology.

### 6.7 Protections

`ProtectionInput` is independent and contains UID, Owner, Protects, protection kind, role, poles, rated current, breaking capacity, curve, differential current/type, optional manufacturer/model and state information.

Initial kinds include Breaker, Differential, Fuse, Combined, Other and Unknown. Protection ownership and protected entity are distinct references so a board-level main protection and a circuit-level branch protection can be represented without overloading circuit fields.

While the host lacks canonical protection entities, the adapter may synthesize deterministic protection UIDs from canonical circuit UIDs and roles. This is an integration compatibility mechanism, not a requirement for the UI engine to know host details.

### 6.8 Buses

`BusInput` may be explicit, but V1 may deterministically synthesize `BUS:<board>:MAIN` when a host does not model buses independently. A synthetic bus is valid diagram semantics and does not imply persistent creation in the host project.

### 6.9 Grounding

`GroundingInput` contains UID, owner reference, grounding kind, conductor information, optional measured resistance, measurement metadata and states. Missing grounding data remains missing; it is never converted to numeric zero.

### 6.10 Results and state model

`ElectricalResultInput` contains only result values required by the drawing, such as installed power, theoretical/design current, corrected ampacity, voltage drop, global status and execution/result state.

Three state concepts remain separate:

```text
OperationalState: Active | Inactive | Unknown
DataState: Complete | Incomplete | Invalid | Unknown
ResultState: Current | Stale | Pending | Missing | Unknown
```

The renderer may format state differently, but it cannot collapse absence, pending calculation and stale result into the same semantic value.

## 7. Input validation

`SingleLineInputValidator` returns typed validation issues with at least Error and Warning severity.

Errors include duplicate UIDs, missing referenced entities, invalid circuit ownership, invalid supply references, self-supply, cycles, incompatible identity use and invalid protection references.

Warnings include incomplete source data, missing breaker information, missing conductor information, incomplete grounding, stale results, unresolved legacy feeder information and boards without resolved supplies.

Errors may block all or a relevant part of projection. Warnings do not block visualization. The engine must be capable of drawing explicit Unknown endpoints for incomplete but structurally coherent data.

## 8. Semantic projection

`SingleLineProjectionBuilder` converts validated input into a renderer-independent semantic drawing projection. Projection contains no coordinates, sizes, colours or font data.

`SingleLineProjection` contains:

```text
ProjectUid
SummaryProjection
BoardDetailProjection[]
ProjectionIssue[]
InputFingerprint
```

### 8.1 Summary projection

The summary represents sources, boards and supply relationships compactly. It does not expose every final circuit. It includes `SummaryNode[]`, `SummaryConnection[]`, roots and issues.

Summary connections retain their `SupplyConnectionUid`, origin, optional through-circuit, destination, role and state so a downstream board can be traced to the actual feeder relation.

### 8.2 Board-detail projection

Each board detail contains board metadata, incoming supplies, main bus, branches, grounding, issues and navigation targets.

`BranchProjection` contains circuit reference, protection chain, conductor, relevant electrical results, destination and branch kind. Initial branch kinds are FinalCircuit, DownstreamBoard, Spare and Unknown.

Protection chains are variable-length collections. No renderer assumes a fixed `TM + ID` structure.

A downstream-board destination carries an explicit navigation target.

### 8.3 Projection ordering and tolerance

Projection order is deterministic. Board/circuit ordering must use explicit semantic ordering and stable identity as a final tie-breaker, never incidental dictionary/list enumeration.

Unknown or incomplete data produces explicit issues and Unknown semantic representations rather than renderer exceptions.

## 9. Drawing profile and provenance

`RIC18DrawingProfile` is an immutable versioned graphic profile. It decides how projection semantics are composed into technical drawing blocks, not what exists electrically.

The profile contains profile/version identity, symbol catalog, block catalog, line styles, text styles, layout constants and provenance records.

Every graphic rule is classified as exactly one of:

- `RIC18_EXPLICIT`
- `RIC18_CATALOG`
- `RIC18_REFERENCE`
- `APP_CONVENTION`

No application convention may claim RIC authority. Exact regulatory content and exact citations must be verified from authoritative source documents before a rule can be classified as explicit/catalog/reference. If that verification is unavailable, the rule remains an application convention or is excluded from the official profile.

Profile data is expected under `data/ric18/v1/`, potentially split into `profile.json`, `symbols.json`, `line-styles.json`, `text-styles.json`, `layout.json` and `provenance.json`. Runtime objects are validated and immutable after loading.

`DrawingProfileValidator` rejects duplicate IDs, invalid anchors, missing referenced styles, invalid bounds/paths, invalid version metadata and missing provenance classification.

## 10. Symbols, blocks and composition

A `SymbolDefinition` is a reusable vector primitive definition with nominal bounds, anchors, drawing primitives, label slots and provenance.

A `BlockDefinition` composes symbols and labels into semantic drawing units. Initial blocks include SourceBlock, ServiceEntranceBlock, IncomingSupplyBlock, MainProtectionBlock, MainBusBlock, CircuitBranchBlock, ProtectionChainBlock, DownstreamBoardBlock, FinalLoadBlock, GroundingBlock and UnknownBlock.

The composition boundary is:

```text
SingleLineProjection
        ↓
CompositionBuilder + RIC18DrawingProfile
        ↓
DrawingComposition
        ↓
LayoutEngine
```

Composition decides what pieces exist. Layout decides where they are placed.

Initial symbol coverage includes source/utility, service entrance, breaker, residual-current device, fuse, bus, grounding, connection node, downstream board, final load and unknown endpoint. Additional equipment is a later extension point.

## 11. DiagramScene and geometry

`DiagramScene` is a neutral vector scene. One internal unit equals one millimetre of drawing space.

It contains scene identity/kind, bounds, elements, connections, layers, issues and metadata. Initial neutral primitives include line, polyline, rectangle, circle, path, text, symbol instance and group.

Every relevant scene element has a deterministic `SceneId`, semantic reference, bounds, layer and stable z-order. Random per-render IDs are prohibited.

Initial semantic layers include Background, Power, Protection, Grounding, Symbol, Text, Annotation, StatusOverlay and Interaction, although export/interactive visibility can suppress interaction-only content.

`DiagramSceneValidator` checks finite geometry, positive dimensions where required, unique IDs, valid anchors, valid connection endpoints and declared bounds. Strict layout validation additionally checks prohibited structural overlaps.

## 12. Anchors and orthogonal routing

Symbols/blocks expose semantic anchors such as PowerIn, PowerOut, BusTap, Ground, Reference and Annotation. Connections refer to anchors, not arbitrary coordinates.

`OrthogonalConnectionRouter` produces horizontal/vertical polylines using scene obstacles and routing policies. Structural electrical connections do not use automatic diagonal routing in V1.

Connection compatibility is represented semantically and later reused by interaction. Valid and invalid anchor-role combinations are exhaustively tested where the state matrix is small.

## 13. Layout engine

`SingleLineLayoutEngine` has at least two strategies: `SummaryLayoutStrategy` and `BoardDetailLayoutStrategy`.

Summary layout uses electrical depth and descendant structure to create compact hierarchical placement. Primary topology establishes the main tree/forest; alternative/emergency/bypass supplies are routed as supplementary connections so a second source does not destroy the base layout.

Board-detail layout uses a technical template: incoming supply, main protection, main bus and branch columns. Each branch measures its real protection chain, labels and destination before placement.

The layout pipeline is:

```text
Measure → Place → Route → Resolve collisions → Validate → Scene
```

Text measurement is abstracted through `ITextMetrics`; deterministic test metrics are available so geometry tests do not depend on operating-system font rendering.

`LayoutProfile` owns spacing/padding/grid values. No renderer or window code hard-codes technical coordinates. Values without explicit normative authority remain `APP_CONVENTION`.

### 13.1 Manual layout and stability

`DiagramLayoutState` stores presentation metadata only. `LayoutOverride` supports Auto, Pinned and Locked modes. Electrical topology never appears in layout state.

Incremental layout receives previous scene/layout state and attempts to preserve existing positions, then introduce new elements and resolve collisions. A full automatic relayout remains available.

Large board details may wrap branches into continuation rows. Wrapping is a layout decision, not a PDF-specific hack.

## 14. Avalonia renderer

`UI_Unilineal.Rendering.Avalonia` uses a custom `SingleLineView : Control` with immediate drawing through Avalonia's drawing context rather than creating one Avalonia control per scene primitive.

The renderer owns only drawing, viewport transformation, hit-testing integration and pointer/keyboard interaction translation. It does not own project navigation, inspector content, command execution, persistence or electrical rules.

### 14.1 Viewport

`ViewportState` contains zoom, pan, bounds and zoom limits. Zoom/pan operate as transformations from scene millimetres to Avalonia DIPs; they do not mutate scene geometry or trigger layout.

Required viewport functions include ZoomAt, Pan, FitScene, FitSelection, ActualSize and CenterOn. A zoom-at-cursor operation preserves the scene point under the pointer.

### 14.2 Culling, caches and ordering

The view queries a spatial index for visible elements and renders only the visible subset. Symbol geometry, paths, pens/brushes and formatted text may be cached with bounded caches and profile-aware invalidation.

Render ordering is deterministic by layer, z-index and scene ID.

### 14.3 Hit testing

Hit testing converts pointer DIPs through the inverse viewport transform into scene millimetres and queries a scene spatial index. Visual stroke width and hit tolerance are separate so thin technical lines remain selectable.

Selection priority is deterministic, preferring interactive anchors and semantic symbols over broad containers/background.

### 14.4 Interaction overlays

Selection outlines, hover, connection previews, handles, drag ghosts and marquee selection are rendered as interaction overlays and never stored in `DiagramScene` or exported by default.

Interaction is an explicit state machine rather than a collection of independent booleans. Initial states include Idle, Hovering, Selecting, Panning, DraggingLayout, ConnectingElectrical, MarqueeSelecting and CommandPreview. Escape cancels cancelable interactions and returns to a stable state.

## 15. Interaction and commands

The command model has two families:

```text
IDiagramCommand
├─ LayoutCommand
└─ ElectricalCommand
```

Layout commands affect presentation only. Initial examples include MoveEntity, MoveGroup, PinEntity, LockEntity, ResetEntityPosition, ResetBoardLayout and ResetAllLayout.

Electrical commands express user intent and are never executed by the renderer. Initial command concepts include ChangeBoardSupply, ChangeSupplyCircuit, ChangeProtection, ChangeConductor, ChangeCircuitData, CreateSupplyConnection and RemoveSupplyConnection. Creation/deletion of entire boards/circuits is not required to be duplicated inside the single-line workspace if the host's existing editor is the safer route.

### 15.1 Explicit electrical editing

Dragging a board never reconnects it. Electrical connection editing starts from semantic anchors in explicit Electrical mode. A connection gesture produces an `ElectricalCommandProposal`, not an immediate mutation.

Command impact is classified as PresentationOnly, LocalElectrical, CascadingElectrical or Destructive. Cascading/destructive changes require confirmation and may display host-provided consequences before execution.

### 15.2 Host command contract

The host implements `IElectricalCommandHandler`. Execution returns a typed `CommandResult` such as Applied, Rejected, NeedsConfirmation, Conflict or Failed, together with changed/invalidated entities, diagnostics and a new input snapshot/revision where applicable.

Electrical editing is not optimistic in V1. The flow is propose → validate → execute in host → rebuild input → reproject. Layout dragging may be visually optimistic because it cannot corrupt electrical topology.

### 15.3 Undo/redo

Layout and electrical histories are separate. Layout commands are locally reversible. Electrical undo submits an inverse electrical command to the host and is subject to current validation and revision checks. An undo that is no longer valid returns Conflict rather than forcing stale state.

## 16. Navigation and inspector

The validated interaction model is Project Summary → Board Detail → Downstream Board Detail → Back.

Navigation state is outside the renderer and may use routes equivalent to ProjectSummary, BoardDetail(boardUid) and entity-focus routes. The shell owns the navigation stack.

The inspector is also outside `SingleLineView`. It is built from selection using entity-specific inspector models for boards, circuits, protections, supplies and issues. This allows the drawing to remain technically compact while still exposing detailed information.

## 17. Host capabilities

The UI consumes explicit `HostCapabilities` rather than assuming every environment supports every edit. Capabilities may include layout editing, electrical editing, circuit creation/deletion, protection editing, export and layout persistence.

Read-only integration remains fully useful. Capabilities may be enabled progressively as the host obtains canonical data required for safe write operations.

## 18. Document composition and exports

`DiagramScene` is logical drawing geometry. `DrawingDocument` is one or more physical sheets containing that geometry. SVG/PDF are serializers of `DrawingDocument`.

```text
DiagramScene
    ↓
DocumentComposer
    ↓
DrawingDocument / DrawingSheet[]
    ├─ SvgExporter
    └─ PdfExporter
```

`DrawingSheet` records paper size in millimetres, orientation, scale, view box, margins, title block and scene viewport. Initial paper presets include A0–A4 and Custom.

Screen zoom and document scale are independent. Fit-to-sheet may not reduce below a configured minimum legibility threshold; when it cannot fit, the composer chooses a larger sheet or continuation strategy according to policy.

Per-board detail naturally prefers one board per sheet, with deterministic continuation sheets for large boards. Continuation references are document entities, not PDF-only text hacks.

### 18.1 SVG

SVG is the reference vector exporter for regression. It is deterministic, self-contained, preserves semantic grouping where useful and contains no executable scripts, external CSS, remote fonts or remote images. Project text is escaped safely.

### 18.2 PDF

PDF is vector-first and multipage. It must support required vector primitives, clipping, text, physical page dimensions, line widths and dash patterns. Screen capture and silent raster fallback are prohibited. If the selected PDF backend cannot satisfy those gates, release is blocked rather than silently degrading output.

### 18.3 Styles and fonts

A shared style resolver converts semantic profile styles into resolved line/text/fill styles before exporters interpret them. Interactive light/dark themes are separate from the technical print theme.

Text should remain text in SVG/PDF where the backend permits reliable metrics. Strict export validates required fonts rather than silently substituting a layout-changing font.

### 18.4 Export traceability

`ExportManifest` records project UID, input/projection/scene/profile fingerprints, drawing profile ID/version, layout engine version, exporter version, document revision and sheet count. Semantic fingerprints are separated from binary file hashes so timestamps/metadata do not make semantically identical documents look different in regression testing.

Export performs preflight validation before writing and supports atomic file replacement at the host boundary. Low-level exporters write to streams and accept cancellation.

## 19. Integration with ProyectoElectrico

`ProyectoElectrico` remains the sole canonical electrical source of truth. Integration is implemented host-side in a dedicated anti-corruption project such as `ProyectoElectrico.V6.Integration.SingleLine`.

The host provides at least:

- `ISingleLineInputProvider`
- `IElectricalCommandHandler`
- `IDiagramLayoutStore`

The adapter reads through `OperationalProjectSession`/Application services rather than reading CSV or `IProjectStore` directly. Writes also go through Application use cases/session APIs so existing validation and calculation invalidation are reused.

### 19.1 Stable host identity

Canonical `Guid Uid` values from host boards/circuits cross the integration boundary. Numeric database/CSV IDs remain host implementation details. The integration layer may keep a revision-scoped `ProjectEntityIndex` mapping UI UIDs back to host IDs for command execution, but these numeric IDs never enter `SingleLineInput`.

### 19.2 Legacy hierarchy gap

Current host `ParentBoardId` establishes a board hierarchy but does not identify which parent circuit feeds the child board. V1 must not infer that circuit from code, position, name or heuristics.

Legacy projects may therefore produce a `LegacySupplyHint`/unresolved-supply issue: parent board known, feeder circuit unknown. They remain visualizable with explicit warning/unknown endpoints.

### 19.3 Canonical supply connection in host

Full topology editing in the integrated V1 requires a canonical persisted relation equivalent to `SupplyConnection` in `ProyectoElectrico`. A host representation may support either a source origin or an origin circuit and a destination board, with supply role, priority and normally-active state.

Existing `ParentBoardId` remains for backward compatibility during the first integration and may be synchronized from the canonical normal supply where required by legacy host functionality. It is not the long-term electrical truth.

Migration is explicit and non-destructive. Opening a legacy project creates only in-memory hints/synthetic entities. The user resolves an unknown feeder by selecting the real feeder and confirming creation of the canonical supply relation.

### 19.4 Synthetic integration entities

Until host entities exist, deterministic synthetic source, bus and protection identities are permitted. Synthetic entities never imply automatic persistent host mutations.

### 19.5 Revisions and stale-edit protection

The adapter computes a deterministic input revision fingerprint from ordered relevant host semantics. Electrical commands carry `ExpectedRevision`. If the current revision differs, the handler returns Conflict and performs no mutation.

After successful execution the host rebuilds the canonical read state and adapter output. The diagram consumes the new snapshot rather than patching its own topology.

### 19.6 Layout persistence

Layout is persisted separately from electrical entities, for example as a versioned `diagram_layout.json` dataset inside the project container. Missing or corrupt layout must never prevent the electrical project from loading; auto-layout can regenerate presentation.

Opening/viewing/closing without an explicit edit must not modify project files.

### 19.7 Packaging

Long-term integration consumes versioned UI_Unilineal packages/contracts rather than a floating Git branch or Git submodule. SemVer is used for public package contracts. During development, a local package feed may be used.

## 20. Testing strategy

Reliability is expressed as reproducible evidence, not an invented percentage.

### 20.1 Test levels

V1 uses:

- deterministic unit tests for local contracts and invariants;
- property-based tests for broad semantic/layout invariants;
- model-based state-machine tests for interaction/undo sequences;
- exhaustive matrices where the state space is small;
- pairwise/3-wise combinatorial coverage for configuration interactions where exhaustive products are redundant;
- contract/golden tests for stable transformations and serialized outputs;
- integration/round-trip tests for host adapters and commands;
- E2E tests for golden user flows;
- architecture tests for forbidden dependency directions;
- stress/performance/memory regression tests;
- mutation testing on critical validators/topology/state logic.

### 20.2 Core invariants under test

Required evidence includes:

- no duplicate semantic identities;
- no dangling references;
- no invalid supply cycles/self-supply;
- deterministic output for identical input/profile/layout state;
- equivalent output for semantically equivalent shuffled input collections;
- finite positive geometry and valid anchors;
- prohibited structural blocks do not overlap in validated layouts;
- orthogonal routing where required;
- locked/pinned layout invariants;
- layout dragging cannot create an electrical command;
- electrical reconnect requires semantic anchors and explicit Electrical mode;
- rejected/conflicting commands do not mutate canonical state;
- no-write-on-open for integrated legacy/current projects;
- SVG contains no executable/external content;
- PDF/SVG consume the same drawing document semantics;
- architecture references remain one-directional.

### 20.3 Combinatorics

For `N` binary dimensions the exhaustive space is `2^N`; for heterogeneous dimensions it is the product of each state count. V1 does not attempt blind global Cartesian-product enumeration.

Small finite compatibility matrices are tested exhaustively. Numeric domains use equivalence classes and boundaries. Many independent flags use pairwise or 3-wise coverage. Graph topology uses property generation/shrinking. Sequential interaction uses model-based state generation. Historical bugs always receive regression tests before fixes.

### 20.4 Golden fixtures

Canonical fixtures include at least MinimalProject, SimpleBoard, NestedBoards, MultipleSupplies, MissingData, LegacyHierarchy, InvalidCycle, LargeBoard24, LargeBoard48 and a larger project fixture.

Structural projection/scene/SVG goldens are preferred over pixel snapshots. A small number of visual regression fixtures cover symbol gallery, summary and board detail for clipping/contrast/rendering regressions.

Golden changes are reviewed deliberately; bulk automatic snapshot acceptance is prohibited.

### 20.5 CI and regression gates

Normal PR/push CI executes restore, format/lint where configured, Release build, unit/property/architecture/contract/integration tests and SVG goldens. Nightly executes extended property runs, stress, mutation and performance suites. Release candidates execute all of the above plus package validation, deterministic export, PDF smoke/structural checks and clean-install/clean-clone validation.

`TreatWarningsAsErrors` remains enabled. A production checkpoint is green only when previous tests and new tests are both green, no unexpected golden differences exist and the intended tree/diff is clean.

Performance thresholds are established from measured baselines rather than invented before measurement. Stable benchmarks may fail on statistically meaningful regressions; unstable measurements are diagnostics until a reliable threshold is established.

## 21. Failure handling and isolation

Failures are typed at subsystem boundaries: input validation, projection, profile validation, layout, scene validation, export validation, integration conflict and command rejection.

A failure in the single-line subsystem must not prevent the host project from performing unrelated electrical workflows. A corrupt drawing profile or layout document may disable the single-line workspace with diagnostics, but it must not corrupt or block core project loading, calculation or editing.

No renderer catches invalid domain state by silently fabricating electrical values.

## 22. Observability

Development diagnostics may expose input/projection/scene/profile fingerprints, entity/primitive counts, layout duration, index duration, render duration, hit-test duration and cache statistics. This is local diagnostic instrumentation, not mandatory external telemetry.

Structured logging is preferred over ad-hoc console output.

## 23. V1.0 functional acceptance

V1.0 is accepted only when a compatible fixture can complete the following flow from a clean build:

```text
load semantic project
→ validate
→ show project summary
→ open board detail
→ navigate to downstream board
→ select/inspect entities/issues
→ zoom/pan/fit
→ edit layout
→ undo/redo layout
→ persist/reload layout
→ execute at least the enabled safe electrical command set through a host contract
→ receive rebuilt input/reprojection
→ export deterministic SVG
→ export vector multipage PDF
```

For full `ProyectoElectrico` integration, the equivalent flow must work on a real canonical `.peproj` through Application-level APIs, preserve stable UIDs, support legacy read compatibility, provide canonical supply relations for topology edits, and remain consistent after save/close/reopen.

## 24. Implementation gates

Implementation is incremental. Every gate must retain all previous green evidence before the next begins.

### G0 — Baseline and CI

Record clean `main` restore/build/test baseline; establish CI and regression artifact capture.

### G1 — Domain foundation

Implement identity, semantic inputs, explicit supply topology, protection/grounding contracts and input validation using TDD/property tests.

### G2 — Projection

Implement summary/detail projections, issues, deterministic ordering and projection goldens.

### G3 — Graphic profile and composition

Implement versioned RIC18 drawing profile structures, vector symbol/block definitions, provenance validation and composition.

### G4 — Scene foundation

Implement neutral primitives, anchors, deterministic IDs/layers and scene validation.

### G5 — Layout

Implement summary/detail layout, deterministic text metrics, orthogonal routing, collision rules, manual overrides, incremental stability and branch wrapping.

### G6 — Avalonia

Implement immediate-mode `SingleLineView`, viewport, culling, spatial hit-testing, overlays and navigation shell integration in Playground.

### G7 — Interaction

Implement explicit modes, interaction state machine, layout commands/history, electrical proposals, host capabilities and inverse-command contracts.

### G8 — Documents and exports

Implement document composition, pagination/title blocks, SVG exporter, PDF exporter, preflight and export traceability.

### G9 — Hardening

Expand generative/combinatorial/model-based tests, stress/performance baselines, mutation testing of critical logic and cross-platform deterministic checks where feasible.

### G10 — ProyectoElectrico read integration

Implement host-side adapter, UID mapping, revision fingerprint, legacy compatibility, host navigation and isolated layout persistence. Keep electrical editing disabled until read integration is proven.

### G11 — ProyectoElectrico write integration

Introduce canonical host supply connection, Application-level translators/handlers, revision-aware command execution, calculation invalidation/recalculation policy and round-trip tests.

### G12 — Release

Run full regression, compatibility, performance and export evidence; complete docs/package/release notes; produce release candidates and then 1.0.0 only after all acceptance gates pass.

## 25. Implementation decomposition

This architecture is intentionally larger than one safe implementation batch. It is a master design, not permission to produce one unreviewable mega-commit.

The implementation shall be decomposed into independently verifiable work packages. At minimum:

1. Core semantic pipeline: G0–G2.
2. Graphic model and scene/layout: G3–G5.
3. Interactive Avalonia workspace: G6–G7.
4. Documentation/export pipeline: G8.
5. Hardening and reliability: G9.
6. `ProyectoElectrico` read integration: G10.
7. `ProyectoElectrico` write/topology integration: G11.
8. Release stabilization: G12.

Each package receives a detailed implementation plan before coding that package. Architectural assumptions discovered to be false upgrade back into design review rather than being patched around. This decomposition is required to preserve the user's original constraint: every addition must demonstrate that it did not break accepted earlier behaviour before the next layer is introduced.

## 26. Commit and branch strategy

Production work occurs on `feature/v1-single-line-engine`, created from clean `main`, not from any V0.x spike branch. Commits are small, coherent and bisectable; each implementation gate ends in an explicit green checkpoint.

`ProyectoElectrico` host integration starts later on its own feature branch only after UI_Unilineal public contracts are sufficiently stable. Host integration pins an explicit package/version or local package artifact rather than consuming a floating branch.

## 27. Release progression

Production version progression may use `1.0.0-alpha.*`, `1.0.0-beta.*`, `1.0.0-rc.*`, then `1.0.0`.

Alpha allows architecture/API movement. Beta freezes the principal public contracts (`SingleLineInput`, projection/scene contracts and host ports) except for justified corrections. RC permits only release-blocking fixes. V1.0.0 requires all release gates green and documented limitations explicit.

## 28. Final design decision

V1 is defined as a production single-line engine that is semantic, vectorial, deterministic, traceable, safely editable, exportable, extensible and verifiable. It covers the validated hybrid summary/detail workflow, explicit electrical topology, incomplete/legacy visualization, safe host-mediated edits, deterministic technical layout, Avalonia interaction and vector documentation without turning the drawing subsystem into an independent electrical database or a general CAD platform.

The success criterion is not feature count. The success criterion is that accepted semantics and user flows are supported by clean boundaries and reproducible evidence, and that each later capability can be added without silently invalidating the behaviour already established by earlier gates.
