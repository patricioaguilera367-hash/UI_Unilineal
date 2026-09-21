# R2 — ProyectoElectrico board diagram read model

Status: **IMPLEMENTED IN HOST BRANCH — AWAITING CI RUNNER AVAILABILITY**

Host repository:

- `patricioaguilera367-hash/ProyectoElectrico`
- branch: `feature/single-line-supply-topology`

## Implemented

ProyectoElectrico now contains the canonical supply relation required by R1:

```text
BoardSupply
├── FeederCircuitId
├── DestinationBoardId
├── Role
├── Priority
├── IsNormallyActive
├── State
└── DataState
```

The relation supports:

- one feeder circuit -> many downstream boards;
- one downstream board <- many feeder circuits;
- future alternate/emergency source topology without implementing generators now.

The project schema was advanced to v2 with `board_supplies.csv`. Schema v1
projects are read compatibly as v2 with an empty supply collection and are
materialized as v2 on their next save. No supply relationship is inferred from
`ParentBoardId`.

Validation now covers:

- project/circuit/board FKs;
- stable ID/UID identity;
- duplicate feeder/destination pairs;
- self-supply;
- non-negative priority;
- normally-active supply cycles.

## Host read model

ProyectoElectrico now also exposes a read-only:

```text
BoardDiagramReadModel
├── Board
├── IncomingSupplies[]
├── MainBus (structural only unless canonical ratings exist)
├── Branches[]
│   ├── circuit identity/status
│   ├── adopted/recommended calculation display
│   ├── neutral presence: Present only when canonically supported, else Unknown
│   ├── PE presence: Unknown until modeled
│   ├── differential presence: Unknown until modeled
│   ├── Loads[]
│   ├── DownstreamBoards[]
│   └── RequiresJunctionBus
└── Issues[]
```

Important: the read model explicitly preserves unknown information instead of
inventing it.

## Evidence fixture

A canonical persisted sample was added:

`samples/SingleLineTopologyDemo.peproj`

It contains:

- `TGBT-01-C01` feeding three downstream boards;
- `TDA-01` receiving a second, normally inactive alternate feeder;
- no generator/ATS object yet.

Tests were added for:

- fan-out;
- multiple incoming supplies;
- duplicate relation rejection;
- active cycle rejection;
- CSV round-trip;
- schema v1 -> v2 compatibility;
- projection of the persisted topology sample.

## CI note

GitHub Actions runs on the ProyectoElectrico feature branch are currently
ending before runner steps start (jobs report no executed steps). This is not
being treated as GREEN evidence. The implementation must remain unmerged until
a real Windows + Linux run executes and passes.

## Next route

R3 now owns the next product risk:

> render `BoardDiagramReadModel` with a deterministic board grammar where
> branch fan-out is always represented by an explicit junction bus with nodes,
> never by line-to-line touching.

Do not reopen generic electrical canvas editing while R3/R4 remain open.
