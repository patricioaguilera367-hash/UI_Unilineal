# R1 topology decision — multi-supply and bus-only junctions

Status: **ACCEPTED**

Date: 2026-09-21

Applies to:

- `ProyectoElectrico` as the canonical electrical source of truth.
- `UI_Unilineal` as a read-only graphical projection during V2.

## Decision

The product must support these two topology cases:

1. A board may have more than one incoming supply.
2. One feeder circuit may intentionally supply more than one downstream board.

Generator-specific behavior is **not** part of the current implementation scope,
but the canonical topology must not make it impossible to add generators,
emergency sources or transfer schemes later.

Therefore a single nullable field such as:

```text
Circuit.DownstreamBoardId
```

is rejected. It cannot represent either requirement correctly.

## Canonical topology direction

Use a separate canonical supply relation in ProyectoElectrico.

Current minimum concept:

```text
BoardSupply
├── Id / Uid
├── ProjectId
├── FeederCircuitId
├── DestinationBoardId
├── SupplyRole
├── Priority
├── IsNormallyActive
├── State
└── DataState
```

This is a relation, not a second board or circuit model.

Cardinality:

```text
Circuit 1 ─────< BoardSupply >───── 1 Board
```

which permits:

```text
one Circuit -> many BoardSupply rows -> many downstream Boards
one Board   <- many BoardSupply rows <- many incoming Circuits
```

### Current validation intent

For a board-fed supply:

- `FeederCircuitId` must reference an existing circuit.
- `DestinationBoardId` must reference an existing board.
- feeder circuit and destination board cannot create an invalid active supply
  cycle;
- duplicate equivalent active supply rows should be rejected;
- hierarchy fields such as `ParentBoardId` must not be used as a substitute for
  the exact feeder relation.

Whether `ParentBoardId` remains as a navigation/grouping hierarchy after
`BoardSupply` becomes canonical is a separate migration decision. Do not make
the diagram depend on `ParentBoardId` for electrical topology.

## Future sources

Do **not** implement generator/utility source entities now merely to make the
schema look future-proof.

When the product later needs sources such as:

- utility service;
- generator;
- UPS;
- emergency source;
- transfer/ATS arrangements;

extend the canonical supply model deliberately. The current `BoardSupply`
must be designed so this can be migrated without changing diagram semantics.

The immediate V2 target is circuit-origin supply topology only.

## Graphic invariant: every union/split uses a bus

The single-line visual grammar has a strict invariant:

> Electrical lines never merge, split or touch each other directly. Every
> multi-line union or distribution point is represented by a bar/bus with
> explicit connection node(s).

Examples:

### One feeder, one downstream board

```text
feeder
  |
  ●  connection to the relevant bus/axis
  |
board
```

### One feeder, several downstream boards

```text
            feeder
              |
        ======●======   junction/distribution bus
           ●     ●
           |     |
        Board A Board B
```

No T-junction made only from touching line segments is allowed.

### Several incoming supplies to one board

Each incoming path remains a distinct semantic path and connects through the
appropriate board/bus structure. Lines do not overlap or merge by coincidence.

## Persisted fact vs generated graphic element

A visual junction bus created only to express a one-to-many connection does
**not** automatically need to be stored as an electrical entity.

If several `BoardSupply` rows share the same feeder circuit, the diagram may
derive a `JunctionBus`/distribution bar as deterministic view grammar:

```text
same FeederCircuitId
        |
        v
generated JunctionBus
   |       |       |
 supply  supply  supply
   |       |       |
 board   board   board
```

That generated bus carries no invented electrical rating, section or capacity.

If later the user needs to assign engineering properties to that bus, it must
then become a canonical domain entity in ProyectoElectrico.

## Consequence for BoardDiagramModel

The V2 read model must not assume:

```text
branch -> single destination
```

It must support:

```text
branch
├── zero downstream boards -> final/load circuit
├── one downstream board
└── many downstream boards -> generated junction bus + destinations
```

Incoming supplies must likewise be a collection, not a single field.

Conceptually:

```text
BoardDiagramModel
├── IncomingSupplies[]
├── MainBus
└── Branches[]
    └── Destinations[]
```

## Consequence for layout

This decision strengthens the V2 layout strategy:

- known electrical topology is laid out by explicit grammar;
- fan-out is represented by a real bar with nodes;
- the generic orthogonal router is not responsible for inventing junction
  topology;
- nodes are semantic connection points, not accidental crossings;
- line crossings, if ever visually unavoidable, must not imply connection.

## Out of scope now

- generator modeling;
- ATS/transfer logic;
- source synchronization;
- protection selectivity between alternate sources;
- bus electrical ratings;
- graphical editing of supply topology.

Those may be added later without changing the accepted rule that topology is
canonical and visual unions occur only through explicit buses/nodes.
