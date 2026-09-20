# RIC 18 geometry calibration plan

Date: 2026-09-20  
Branch: `feature/ric18-graphic-grounding`

## Objective

Replace the current generic board-detail geometry with a deterministic RIC-18-oriented geometric grammar calibrated against the supplied reference DXF and visual RIC 18 sheet.

This work is about **visual structure and geometry**, not about claiming that the DXF dimensions are normative.

## Scope rule

**Do not change Project Summary yet.**

First make `BoardDetail` visually coherent and stable. Only after the board detail reaches the acceptance criteria below should the same grammar be reused to replace the generic summary boxes.

## Reference assets

Reference DXF SHA-256:

`1591ce0774012e009cbe2ab116fac8af7d89a70af28804c97bcfe937d48665a9`

Existing persistent references:

- `docs/reference/ric18-visual-contract.md`
- `data/ric18/reference/visual-contract-v1.json`

## Core problem in the current implementation

`BoardDetailLayoutStrategy` still relies on a generic layout with values such as:

- `MinimumBoardWidthMm = 80`
- `MinimumSlotWidthMm = 34`
- `BoardHeaderHeightMm = 15`
- `InternalVerticalGapMm = 3`

Those values define most of the current appearance. The DXF-derived contract only corrected a few invariants; it does not yet control the visual proportions.

Therefore the task is **not** to add more offsets to the current geometry. The task is to introduce a RIC18 board geometry model and make the layout consume it.

---

# Checkpoint A — canonical DXF fixture

Create a canonical machine-readable reference fixture from the supplied DXF.

The fixture must store normalized relations rather than treating raw DXF dimensions as regulation.

Required normalized reference data:

- board frame aspect ratio;
- incoming axis;
- main bus level;
- PE/TP header zone;
- neutral header zone;
- circuit axes;
- general protection zone;
- branch protection zone;
- differential zone;
- bottom exit zone;
- external destination / circuit-number zone;
- label anchor zones.

For each quantity store:

1. raw measured example value;
2. normalized value relative to board frame;
3. provenance = `DXF_REFERENCE_EXAMPLE`;
4. tolerance used by visual regression tests.

### Acceptance

A unit test can load the reference fixture and verify that the measured DXF-derived normalized values are internally consistent.

---

# Checkpoint B — RIC18 board geometry model

Introduce a layout model such as:

```text
Ric18BoardGeometry
├── Frame
├── Header
│   ├── ProtectiveEarthZone
│   ├── IncomingAxis
│   └── NeutralZone
├── MainProtectionZone
├── MainBusLevel
├── BranchArea
│   ├── CircuitAxes[]
│   ├── ProtectionLevel
│   ├── DifferentialLevel
│   └── ExitLevel
└── ExternalDestinationLevel
```

The geometry model must own layout ratios/tokens. `BoardDetailLayoutStrategy` must stop owning arbitrary visual constants.

The model may scale to content, but ratios are calibrated against the reference fixture.

### Circuit-axis rule

For N circuits:

- one circuit: axis = board center;
- odd N: middle circuit = board center;
- even N: board center lies halfway between the two central circuits;
- all axes are deterministic and symmetric;
- labels do not alter electrical axes.

### Acceptance

Tests for N = 1, 2, 3, 4, 5, 6 and stress counts remain deterministic and preserve the expected symmetry.

---

# Checkpoint C — rebuild BoardDetail from zones

Refactor `BoardDetailLayoutStrategy` so it places components by semantic zones instead of cumulative generic vertical gaps.

Target ordering:

```text
incoming / empalme       external above frame
          │
board header
 TP       │       N
          │
main protection
          │
==========●==========    main bus
      │   │   │
      │ protections
      │ differentials
      │
board bottom
      │
numbers / loads / downstream boards   external below frame
```

The board frame must be derived from the semantic contents and reference ratios, not from `MinimumBoardWidthMm = 80`.

### Acceptance

For the canonical three-circuit fixture:

- incoming power axis is centered;
- main protection is centered;
- main bus is at the calibrated normalized level;
- circuit axes approximate the DXF normalized positions within tolerance;
- TP and N occupy calibrated header zones;
- branch protection and differential levels fall within calibrated vertical bands;
- circuit exits are below the differential area;
- final loads/downstream boards are outside the frame;
- no electrical conductor is rerouted merely to make room for a text label.

---

# Checkpoint D — text and annotation discipline

Separate electrical geometry from annotation layout.

Required rules:

- ratings sit beside the symbol they describe;
- text does not cross the primary power axis unless unavoidable;
- board code/name uses a dedicated header area;
- TP/N labels are anchored to their bars;
- destination name/number is outside the board;
- annotation bounds are not allowed to enlarge or displace electrical axes.

### Acceptance

Automated overlap checks cover at minimum:

- text vs main vertical power axis;
- rating text vs breaker geometry;
- board title vs incoming conductor;
- destination label vs destination symbol.

---

# Checkpoint E — geometric comparison against DXF

Add a visual-geometry regression test for a canonical 3-circuit board.

The test compares normalized feature vectors, not pixels:

```text
board.aspect
incoming.x
bus.y
tp.center.x
neutral.center.x
circuit[0..2].x
branchProtection.y
differential.y
exit.y
```

Each feature has an explicit tolerance.

This test is the main guard against returning to a generic-looking layout that still satisfies only semantic invariants.

---

# Checkpoint F — manual BoardDetail gate

Only after A–E are GREEN:

1. run the Playground;
2. open a board detail;
3. compare side-by-side with the RIC 18 visual reference and DXF render;
4. record remaining discrepancies by category:
   - geometry;
   - symbol;
   - line type;
   - typography;
   - annotation;
   - missing semantic element.

Do not fix discrepancies with one-off coordinates. Each correction must map to a geometry token, symbol definition, or annotation rule.

### Manual gate acceptance

The board must be recognizable as the same graphic grammar as the reference before continuing to Summary.

---

# Checkpoint G — Summary reuse

Only after BoardDetail passes the manual gate, replace the current generic `SummaryLayoutStrategy` box graph.

Summary should reuse compact representations derived from the same RIC18 geometry grammar rather than inventing a second visual language.

This checkpoint is intentionally blocked until BoardDetail is accepted.

---

# Stop conditions

Do not:

- regenerate goldens simply to silence failures;
- declare arbitrary DXF dimensions to be regulatory RIC 18 values;
- add another one-off layout offset without a named geometric rule;
- modify Summary while BoardDetail is still visually ungrounded;
- move electrical axes because annotation text is too wide.

Every visual change must be traceable to one of:

- `RIC18_VISUAL_REFERENCE`
- `DXF_REFERENCE_EXAMPLE`
- `NORMALIZED_LAYOUT_DECISION`
