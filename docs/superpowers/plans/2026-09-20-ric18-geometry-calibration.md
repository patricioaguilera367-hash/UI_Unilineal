# RIC 18 visual-grammar grounding plan

Date: 2026-09-20  
Branch: `feature/ric18-graphic-grounding`

## Objective

Replace the current generic board-detail geometry with a deterministic visual grammar that reproduces the relationships shown in the RIC 18 reference image.

**Critical premise:** for this figure, RIC 18 provides the image only. It does not define dimensions, proportions, coordinates, scales or tolerances. The supplied DXF is a manual trace made by the user to help us inspect that image; its dimensions and normalized ratios are not acceptance targets.

This work is therefore about **topology, hierarchy, connection grammar, alignment intent, bars, nodes, symbol relationships and visual organization**. Numeric layout dimensions are UI_Unilineal design decisions.

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

# Checkpoint A — canonical visual-grammar fixture

Create a machine-readable fixture that records the **categorical relationships** observable in the RIC 18 image and made easier to inspect by the hand-traced DXF.

Required grammar facts include:

- incoming supply is upstream of the board;
- incoming conductor establishes the main vertical power axis;
- general protection belongs to that incoming path;
- main distribution bus is horizontal;
- circuit branches leave the bus through explicit junction nodes;
- TP/PE and N are distinct bars/zones;
- branch chains preserve their own vertical power axes;
- neutral and protective-earth paths have different routing semantics;
- differential protection is part of the branch chain when present;
- loads / downstream boards are downstream of their branch;
- physical coincident junctions are rendered once;
- annotations belong to elements but do not define electrical topology.

Raw DXF measurements may remain stored as **diagnostic provenance only**. Do not derive pass/fail tolerances from them.

### Acceptance

A unit test can load the fixture and verify the categorical grammar without asserting any RIC18 dimension, ratio or coordinate.

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

The geometry model must own explicit layout tokens. `BoardDetailLayoutStrategy` must stop owning scattered arbitrary visual constants.

Those tokens are **UI_Unilineal design choices**, selected to reproduce the visual grammar coherently across different circuit counts and content. They are not calibrated as if the hand-traced DXF supplied normative proportions.

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
- main bus occupies the intended structural level below the incoming protection;
- circuit axes are distributed deterministically and symmetrically;
- TP and N occupy the intended left/right header regions;
- branch protection and differential elements remain in the correct vertical order;
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

# Checkpoint E — grammar regression tests

Add regression tests for canonical board-detail fixtures, including a three-circuit case inspired by the reference image.

The tests must compare **relations**, not DXF dimensions:

```text
incoming above board
incoming axis == main protection axis
incoming axis == main-bus input axis
main bus horizontal
TP left of incoming axis
N right of incoming axis
branch taps ordered left-to-right
odd middle branch may share incoming axis
protection precedes differential
neutral traverses differential when applicable
PE bypasses differential
destinations remain downstream/outside as applicable
coincident junctions render once
```

Tests may also enforce UI_Unilineal-owned layout tokens for internal consistency, but those tokens must be labelled as implementation/design decisions rather than RIC18 requirements.

This regression layer prevents a return to the generic layout while avoiding the opposite error of turning the user's hand trace into a dimensional standard.

---

# Checkpoint F — manual BoardDetail gate

Only after A–E are GREEN:

1. run the Playground;
2. open a board detail;
3. compare side-by-side primarily with the RIC 18 reference image; use the DXF render only as an inspection aid for connections/alignment that are hard to read in the raster image;
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
- declare any DXF dimension or normalized ratio to be a RIC 18 value or acceptance target;
- add another one-off layout offset without a named geometric rule;
- modify Summary while BoardDetail is still visually ungrounded;
- move electrical axes because annotation text is too wide.

Every visual change must be traceable to one of:

- `RIC18_VISUAL_GRAMMAR` — categorical relation visible in the reference image;
- `DXF_HAND_TRACE_EVIDENCE` — inspection aid only, never metric authority;
- `UI_LAYOUT_DECISION` — numeric size/spacing/proportion chosen by UI_Unilineal.
