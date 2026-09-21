# UI_Unilineal — MASTER ROUTE

> **AUTHORITATIVE ROADMAP**
>
> This file is the single source of truth for what UI_Unilineal is building next.
> If this file conflicts with README, old G0-G10 plans, spikes, specs, or previous
> implementation notes, **this file wins for sequencing and scope**.
>
> Historical documents remain useful as engineering evidence, but they are not
> authorization to continue their roadmap automatically.

## 0. Current baseline

- Working branch for the new direction: `refactor/v2-integration-first`
- Frozen known-good baseline: `e180de64049b9ed3ce4ead4acad3d3ac0a34c364`
- CI for that baseline: GREEN
- V1 G0-G8 code is preserved. It is not deleted.
- G9/G10 from the old roadmap are **not** the next automatic steps.

The reason for the change is simple: the project built a strong generic drawing
engine before proving the most important product question:

> Can ProyectoElectrico drive a correct, fast, readable RIC-oriented board
> diagram using its real canonical data without maintaining a second electrical
> truth?

V2 answers that question first.

---

## 1. Product goal

UI_Unilineal exists to provide a **live graphical projection** of the same
electrical project already represented by ProyectoElectrico.

The user should be able to move between two primary views of the same project:

1. **Load board / cuadro de cargas** — tabular view.
2. **Single-line diagram / diagrama unilineal** — spatial and hierarchical view.

The diagram is not a second project editor and not a second calculation engine.

### Definition of success

A real ProyectoElectrico project can be opened, any board can be selected, and
its single-line diagram appears immediately with the same current electrical
state as the rest of the application.

When relevant project data changes, the diagram rebuilds from the new canonical
state. It does not maintain competing electrical data.

---

## 2. Architecture decision

The new target flow is:

```text
ProyectoElectrico canonical model
          |
          v
SingleLineReadModel / adapter
          |
          v
BoardDiagramModel
          |
          v
RIC-oriented board grammar
          |
          v
DiagramScene
     /       |       \
    v        v        v
Avalonia    SVG      DXF
```

### What each term means

**Canonical model**  
The real project data. It is the authority. If the diagram disagrees with it,
the diagram is wrong.

**Read model / adapter**  
A translator. It reads ProyectoElectrico and exposes only the data needed for
drawing. It must not invent electrical facts.

**BoardDiagramModel**  
A small immutable description of one board: incoming supply, main protection,
bus, outgoing circuits, protection chain, neutral/PE availability, destination,
status, and display data.

**RIC-oriented board grammar**  
Rules for where electrical things belong relative to each other. Example:
incoming path above the bus, branch axes below the bus, neutral and PE in their
defined zones, protection devices on the branch axis.

**DiagramScene**  
Renderer-neutral vector geometry in millimetres. This is a valuable V1 asset
and remains the common output for screen and export.

---

## 3. Non-negotiable rules

1. **ProyectoElectrico is the electrical source of truth.**
2. UI_Unilineal must not become a second calculation engine.
3. The drawing layer must not silently invent missing electrical topology.
4. Missing/ambiguous data must be represented explicitly as missing/ambiguous.
5. BoardDetail is solved before Project Summary.
6. Read-only visualization is solved before graphical electrical editing.
7. RIC visual references define the graphic language where justified; numeric
   dimensions that RIC does not define remain application design choices.
8. Electrical axes and connections are determined by semantics first;
   annotation text adapts around them, not the reverse.
9. A board diagram is a constrained electrical grammar, not an arbitrary graph.
   Use direct deterministic geometry where the route is known. Use a generic
   router only for genuinely free routing problems.
10. DXF must consume the same `DiagramScene` as Avalonia/SVG. No separate CAD
    geometry generator.
11. No gate is considered complete only because CI is green. Visual acceptance
    against real project data is mandatory for geometry gates.
12. New abstractions are added only when a concrete project case requires them.
13. Electrical lines never merge, split or connect by accidental geometric
    contact. Every multi-line union/fan-out is represented by an explicit
    bar/bus with connection node(s).
14. A feeder circuit may have multiple downstream board destinations, and a
    board may have multiple incoming supplies. The read model and layout must
    preserve those cardinalities.

---

## 4. V1 assets: keep, adapt, freeze

### KEEP AND REUSE

- `DiagramScene` and vector primitives.
- Scene identities and semantic references.
- RIC symbol/profile data, including provenance separation.
- Avalonia immediate-mode renderer.
- Viewport, pan, zoom, fit, spatial index and hit testing.
- SVG exporter.
- Deterministic tests that remain meaningful after V2 integration.
- Useful topology concepts such as explicit feeder-circuit -> downstream-board
  linkage.
- Existing visual-reference and provenance documentation.

### ADAPT

- `SingleLineInput`: reduce its role from independent electrical domain to
  an integration/read boundary if it remains useful.
- Board detail composition: make it consume the minimal real host projection.
- Layout: replace generic placement/routing where the RIC board grammar already
  determines the path.
- Tests: prioritize real-project fixtures and visual/relational invariants.

### FREEZE OUTSIDE THE CRITICAL PATH

Do not delete these now, but do not spend product time extending them until the
read-only real-project board view is accepted:

- Electrical canvas editing.
- Electrical command proposals/host command execution.
- Electrical undo/redo.
- Manual layout persistence and advanced layout editing.
- Generic project-summary graph layout.
- Additional PDF/document-composition features.
- Old G9/G10 sequencing as previously written.

---

## 5. Execution route

### R0 — Freeze and establish V2 baseline

**Purpose:** prevent accidental continuation of the old roadmap.

Required:

- This MASTER_ROUTE exists.
- README points here.
- V1 baseline SHA is recorded.
- New work occurs on `refactor/v2-integration-first`.
- No V1 feature is deleted yet.

**Exit:** there is one unambiguous route.

---

### R1 — Prove the host electrical topology

**Purpose:** determine whether ProyectoElectrico contains enough canonical
information to draw the diagram without inference.

Inspect the real project model and answer, with code references:

- Which board owns each circuit?
- Which circuit feeds a downstream board?
- Which board is fed by which source/board?
- Which protection values are adopted/current?
- How is differential protection represented, if present?
- How is conductor/neutral/PE presence represented?
- Which results are current vs STALE?
- What data is absent and would otherwise have to be invented?

#### Accepted topology decision

The relation must be explicit and canonical, but it is **not one-to-one**:

```text
origin board -> feeder circuit -> one or more downstream boards
one downstream board <- one or more incoming feeder circuits
```

Use a separate canonical supply relation in ProyectoElectrico rather than a
single `Circuit.DownstreamBoardId` shortcut.

Current direction:

```text
BoardSupply
├── FeederCircuitId
├── DestinationBoardId
├── SupplyRole / Priority
├── IsNormallyActive
└── state/data-state metadata
```

Generator/ATS/source entities are future scope. Do not build them now merely
for hypothetical extensibility, but keep this relation migratable to a richer
source model later.

Graphic rule: when one feeder fans out to several destinations, the diagram
derives an explicit junction/distribution bus with nodes. Lines never branch by
touching each other directly.

Decision record:
`docs/integration/R1_TOPOLOGY_DECISION.md`

**Exit:** topology mapping and cardinality decision are written; the next host
change is the canonical supply relation in ProyectoElectrico.

---

### R2 — Minimal integration read model

**Purpose:** feed the renderer from real ProyectoElectrico data as early as
possible.

Create the smallest read-only contract needed for one board, conceptually:

```text
BoardDiagramModel
├── Board identity/display
├── IncomingSupplies[]
├── Main protection
├── Main bus
├── Branches[]
│   ├── Circuit identity/display
│   ├── Protection chain
│   ├── Differential, if real
│   ├── Conductor/display data
│   ├── HasNeutral / Unknown
│   ├── HasPE / Unknown
│   ├── Destinations[]
│   │   ├── final/load destination, or
│   │   └── one or more downstream boards
│   └── result/status
└── Issues
```

Rules:

- immutable;
- generated from canonical host state;
- no persistence of its own;
- no electrical calculations;
- no fabricated bus/protection/topology merely to satisfy the renderer.

**Exit:** at least one real board from ProyectoElectrico can be projected into
this read model.

---

### R3 — BoardDetail V2 grammar

**Purpose:** draw one board correctly before solving everything else.

Replace generic graph-like behavior in BoardDetail with deterministic electrical
grammar.

Primary geometry:

```text
incoming/service
       |
main protection
       |
=======●======= main bus
   |   |   |
  C1  C2  C3
   |   |   |
 protection chain
   |   |   |
 destinations
```

Rules:

- branch power path is an explicit vertical axis;
- main bus is an explicit horizontal structure;
- branch tap positions are deterministic;
- every merge/split/fan-out is expressed through a bus/bar with explicit nodes;
- lines never become electrically connected merely because their geometry
  touches or crosses;
- one branch with multiple downstream boards receives a deterministic
  junction/distribution bus before its destination lines;
- N and PE paths exist only when supported by semantics;
- RCD neutral traversal is modeled only when applicable;
- direct known paths are emitted directly, not discovered by a generic router;
- generic router remains available only for genuinely unconstrained paths;
- labels cannot move electrical axes.

**Exit:** canonical board fixtures for 1-6 circuits and selected larger boards
are visually coherent and satisfy relational invariants.

---

### R4 — Real-project acceptance gate

**Purpose:** prove the product, not only the engine.

Use a non-trivial real/sample ProyectoElectrico project.

Acceptance must include:

- multiple boards;
- feeder -> downstream-board navigation;
- final circuits;
- long labels;
- missing data;
- stale/current results;
- different protection configurations;
- enough circuits to expose spacing problems.

Manual acceptance:

1. open ProyectoElectrico;
2. select a board;
3. diagram appears;
4. select circuit/board from diagram;
5. navigation points to the same real entity;
6. change a relevant project value;
7. rebuild/refresh;
8. diagram and load-board view agree on the current state.

Performance must be measured here. A stopwatch in a structural unit test is not
sufficient. Record actual rebuild/render timings for representative boards.

**Exit:** the owner accepts the board view with real project data.

---

### R5 — DXF export

**Purpose:** create a CAD handoff without turning UI_Unilineal into AutoCAD.

Implement:

```text
DiagramScene -> DxfExporter
```

Prefer CAD-native primitives:

- LINE
- LWPOLYLINE
- CIRCLE
- ARC
- TEXT/MTEXT
- BLOCK/INSERT when symbol reuse is beneficial

Requirements:

- millimetre coordinates;
- predictable layers;
- line types/weights mapped intentionally;
- text remains editable where practical;
- no raster fallback;
- same scene geometry as on screen;
- open/import cleanly in AutoCAD-compatible software.

DWG is not the first target. DXF is the interchange format; DWG can be produced
later through AutoCAD or a compatible library/workflow if needed.

**Exit:** an accepted board detail opens in AutoCAD-compatible software with
editable vector entities.

---

### R6 — Project summary

Only after BoardDetail is accepted.

The summary view represents board hierarchy and feeder relationships using the
same canonical topology. It must not introduce a second visual language that
contradicts BoardDetail.

---

### R7 — Optional editing

Only after visualization + real integration + DXF are stable.

Evaluate separately whether the product genuinely benefits from:

- drag-to-arrange;
- persistent pinned positions;
- creating electrical connections graphically;
- electrical undo/redo.

These are product features, not prerequisites for the single-line viewer.

---

## 6. Test strategy

Tests must answer four different questions.

### A. Semantic tests

"Did we translate ProyectoElectrico correctly?"

Examples:

- feeder circuit points to the expected downstream board;
- inactive entities do not masquerade as active;
- STALE remains STALE;
- missing neutral stays Unknown/Absent according to host semantics.

### B. Geometry relationship tests

"Are the electrical relationships drawn correctly?"

Examples:

- incoming, main protection and bus input share the intended axis;
- circuit tap and branch axis coincide;
- protection order is correct;
- destination is downstream;
- N/PE behavior matches the branch semantics.

Prefer relationships over arbitrary pixel/mm snapshots.

### C. Visual regression

"Did a code change visibly alter an accepted diagram?"

Use a small set of approved canonical renders. A changed golden is not
automatically correct; visual review is required before accepting it.

### D. Performance

"Does it feel live?"

Measure with representative real boards and project navigation, not only
synthetic homogeneous circuits.

---

## 7. Senior-dev decision protocol

When a requested change arrives, classify it before coding:

- **REQUIRED NOW** — blocks R1-R5.
- **USEFUL LATER** — valid feature, not on the critical path.
- **EXPERIMENTAL** — preserve as research; do not let it shape core architecture.
- **CONTRADICTS ROUTE** — stop and clarify before implementation.

When the owner requests something technically ambiguous, do not guess silently.
Ask for the missing engineering intent using plain language and, where useful,
show 2-3 concrete alternatives with consequences.

Questions are required when the answer changes any of:

- electrical meaning;
- canonical ownership of data;
- RIC interpretation;
- user-visible hierarchy;
- whether information is inferred or explicitly stored;
- export expectations;
- editability expectations.

For purely implementation-level choices that do not alter those meanings, make
the engineering decision and document it instead of asking unnecessary
questions.

---

## 8. Terminology for non-developers

- **Renderer:** code that draws an already-defined scene on screen.
- **Scene / DiagramScene:** the list of vector objects to draw.
- **Layout:** rules that decide where objects go.
- **Router:** code that finds a path for a connection between two points.
- **Adapter:** translator between ProyectoElectrico data and diagram data.
- **Read model / projection:** a simplified view derived from real data; not a
  second database.
- **Canonical:** authoritative source.
- **Invariant:** a rule that must always remain true.
- **Golden test:** stored expected output used to detect changes.
- **CI:** automated build/test run executed by GitHub.
- **DXF:** CAD exchange format suitable for opening/importing in AutoCAD.
- **DWG:** AutoCAD's native drawing format; not required for the first export.

---

## 9. Immediate next action

R0 is complete. R1 topology is resolved. The ProyectoElectrico host branch now
contains the schema-v2 `BoardSupply` relation, validation/persistence migration,
a canonical topology sample, and the R2 `BoardDiagramReadModel`.

Evidence/status:
`docs/integration/R2_BOARD_DIAGRAM_READ_MODEL.md`.

The next implementation step is **R3**:

> Build the deterministic BoardDetail V2 grammar against the minimal board
> diagram contract. The first required hard case is one feeder circuit with
> multiple downstream boards, represented by an explicit distribution bar and
> nodes.

Do not revive generic electrical editing, project-summary layout, or unrelated
export work while R3/R4 are open.

The ProyectoElectrico host work is not called GREEN until GitHub Actions
actually executes Windows + Linux jobs successfully.

---

## 10. Stop conditions

Stop and ask before continuing if:

- drawing a correct relationship requires guessing a missing electrical fact;
- a proposed convenience would create a second source of electrical truth;
- a RIC image is being treated as if it defined dimensions it does not define;
- the same visual fix requires repeated special cases in layout/router/validator;
- a merge/split would be represented only by touching line geometry instead of
  an explicit bus/node structure;
- the host model and diagram model disagree on entity identity;
- a new feature is expanding G7/G8-style scope before R4 is accepted.

The preferred response to these conditions is not another patch. It is to fix
the model boundary or clarify the engineering rule.
