# RIC 18 diagram-layout principles

Date: 2026-09-20  
Branch: `feature/ric18-graphic-grounding`

## Core framing

UI_Unilineal is not a tracing engine and it is not a pixel-copy of the RIC 18 figure.

It is a **constraint-based diagram layout engine** specialized for electrical single-line diagrams that use the visual grammar shown in the RIC 18 reference image.

The RIC 18 image supplies the domain-specific visual language. UI_Unilineal supplies the actual layout system.

## What the engine is free to decide

UI_Unilineal may choose and tune:

- symbol sizes;
- relative spacing;
- board dimensions;
- circuit pitch;
- header height;
- vertical level spacing;
- annotation offsets;
- line widths;
- minimum clearances;
- expansion rules as circuit count grows;
- compact/expanded variants.

These values are **UI layout decisions**, not RIC 18 measurements.

They should be expressed as named tokens/relations rather than scattered literals.

## What the engine is not free to violate

The following are structural constraints of the visual grammar:

- electrical hierarchy must read correctly;
- upstream elements must appear upstream of downstream elements;
- a board's main supply must reach the intended board entry;
- the main distribution bus is a coherent shared junction structure;
- branch circuits leave the distribution bus through meaningful nodes/taps;
- coincident physical junctions render as one junction;
- TP/PE and N remain distinct systems;
- neutral and protective-earth routing follow their different semantics;
- protections and differential devices appear in the correct chain order;
- branch conductors preserve a coherent power axis unless a routing rule requires otherwise;
- loads and downstream boards appear downstream of the branch that feeds them;
- lines must terminate on explicit anchors/ports, not arbitrary visual points;
- routing must not introduce meaningless detours;
- unrelated conductors must not visually merge;
- geometry, text and symbols must not collide;
- labels must not redefine electrical topology;
- line weights and symbol weights must preserve visual hierarchy;
- the result must remain readable as circuit count and content change.

## Mental model

The engine should behave like a professional flowchart / schematic layout system with domain-specific electrical rules.

```text
Semantic electrical graph
        ↓
Domain-specific composition
        ↓
Constraint layout
        ↓
Port/anchor assignment
        ↓
Orthogonal routing
        ↓
Collision / clearance pass
        ↓
Annotation placement
        ↓
Scene graph
        ↓
Avalonia / SVG / PDF
```

The reference image influences the **constraints and grammar**, not the final numeric coordinates.

## Layout responsibilities

### 1. Semantic graph

Represents what is electrically connected to what.

No pixel or millimeter decisions belong here.

Examples:

- Source → Board
- Main bus → Circuit
- Circuit → Protection
- Protection → Differential
- Differential → Load
- Neutral bus → Differential neutral port
- PE bus → Load PE port

### 2. Composition

Maps electrical semantics into RIC18-specific visual components.

Examples:

- source / empalme assembly;
- board frame;
- incoming protection;
- main distribution bus;
- neutral bar;
- TP/PE bar;
- breaker;
- differential;
- load;
- downstream board.

Composition chooses *which visual structures exist*, not their final coordinates.

### 3. Constraint layout

Determines relative positions.

Examples:

```text
Source above Board
TP left of main axis
N right of main axis
MainProtection on main axis
MainBus below MainProtection
BranchProtection below MainBus
Differential below BranchProtection
Destination below branch chain
```

Prefer relationships such as:

```text
A.CenterX == B.CenterX
A.Bottom + Gap <= B.Top
LeftZone.Right + Clearance <= MainAxis
MainAxis + Clearance <= RightZone.Left
```

over hard-coded absolute coordinates.

### 4. Ports and anchors

Every routed conductor must have explicit semantic endpoints.

Examples:

- MainBus.IN
- MainBus.TAP:C03
- Breaker.IN / Breaker.OUT
- RCD.POWER_IN / RCD.POWER_OUT
- RCD.N_IN / RCD.N_OUT
- Load.POWER
- Load.N
- Load.PE

Routing must begin and end on these ports.

### 5. Orthogonal routing

Routes should be short, legible and semantically meaningful.

Routing priorities:

1. direct straight segment when valid;
2. one-bend orthogonal route;
3. two-bend route around occupied geometry;
4. more complex routes only when required by collisions/topology.

The router must penalize:

- unnecessary bends;
- backtracking;
- crossings;
- running through symbols;
- running through text;
- near-parallel overlaps that look merged;
- routes that leave and re-enter a component without semantic reason.

### 6. Collision and clearance

Geometry must reserve space for:

- symbols;
- conductors;
- connection nodes;
- labels;
- ratings;
- board frame;
- auxiliary PE/N routes.

Clearance is a first-class layout concept.

The engine should detect at least:

- symbol-symbol overlap;
- symbol-line intersection;
- text-symbol overlap;
- text-line overlap;
- text-text overlap;
- route-route ambiguity;
- node rendered inside unrelated geometry;
- route touching board frame except at a defined crossing/port.

### 7. Annotation layout

Electrical geometry is solved first.

Annotations are then positioned relative to their owners using named attachment zones.

Examples:

- rating right of breaker;
- circuit number below exit;
- board code/name in board header;
- TP/N label above its bar;
- load name below load symbol.

Text width may increase annotation space, but should not arbitrarily move electrical axes.

## Visual hierarchy

The resulting diagram must communicate hierarchy before the user reads the text.

At minimum:

1. primary power path;
2. main distribution bus;
3. branch paths;
4. protection symbols;
5. auxiliary N/PE paths;
6. annotations and descriptive text.

Line weights, whitespace, alignment and grouping should reinforce that order.

## Layout tokens

All numeric geometry choices must move toward named tokens, for example:

```text
BoardOuterPadding
HeaderHeight
MainAxisClearance
BusToProtectionGap
ProtectionToDifferentialGap
CircuitPitch
AuxiliaryBusOffset
LabelGap
NodeRadius
PowerLineWidth
AuxiliaryLineWidth
FrameLineWidth
```

Tokens may later be tuned visually without changing semantic code.

No token should be described as a RIC18 dimension unless another explicit source actually defines it.

## Acceptance philosophy

A good result is not one that reproduces the hand-traced DXF coordinate-for-coordinate.

A good result is one where:

- the same electrical hierarchy is immediately understandable;
- connections behave like the visual reference;
- bars/nodes are used coherently;
- routes look intentional;
- nothing collides;
- line hierarchy is clear;
- the drawing scales to different circuit counts;
- the same semantic input always produces the same layout;
- output is consistent across Avalonia, SVG and PDF.

## Test layers

### Structural invariants

Check hierarchy, anchors, topology and ordering.

### Routing invariants

Check endpoints, orthogonality, bend count and forbidden intersections.

### Clearance invariants

Check collisions and minimum separation rules.

### Layout-token invariants

Check internal consistency of UI_Unilineal-owned design tokens.

### Manual visual gate

Compare the rendered board with the RIC 18 image for overall visual grammar.

The hand-traced DXF is an inspection aid when the raster image is ambiguous, never the metric oracle.
