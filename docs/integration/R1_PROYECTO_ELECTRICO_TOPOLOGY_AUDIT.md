# R1 — ProyectoElectrico topology audit

Status: **TOPOLOGY DECISION RESOLVED — READY FOR CANONICAL SUPPLY-MODEL DESIGN**

Audit target:

- Repository: `patricioaguilera367-hash/ProyectoElectrico`
- Branch: `main`
- Audited SHA: `a7b87882d85071d67afe972b4cbbeba1b9843e89`

Purpose: verify whether the canonical ProyectoElectrico model contains enough
information to generate BoardDetail without inventing electrical facts.

## Result

ProyectoElectrico already contains most circuit-level information needed for a
useful read-only diagram. The critical missing relationship is explicit feeder
topology:

```text
upstream board -> exact feeder circuit -> downstream board
```

`Board.ParentBoardId` says which board is hierarchically above another board,
but it does **not** identify which circuit feeds that board.

That gap must be fixed canonically in ProyectoElectrico before UI_Unilineal
claims reliable board-to-board topology.

## Mapping

| Diagram requirement | ProyectoElectrico source | Status | Gap / action |
|---|---|---|---|
| Project identity | `ProjectSnapshot.Project` | EXISTS | Reuse UID/code/name. |
| Board identity | `Board.Id/Uid/Code/Name` | EXISTS | UID is portable identity; code is display only. |
| Board hierarchy | `Board.ParentBoardId` | EXISTS | Useful for hierarchy, insufficient for feeder topology. |
| Circuit belongs to board | `Circuit.BoardId` | EXISTS | Direct FK. |
| Circuit ordering | `Circuit.Number` | EXISTS | Use as canonical branch order unless product later defines another order. |
| Circuit display/service/system | `Circuit.Code/Name/ServiceCode/SystemCode` | EXISTS | Use as display/semantic input, but do not infer topology from names/codes. |
| Exact feeder circuit -> downstream board | No canonical FK | **MISSING** | Critical blocker. Add explicit canonical relationship. |
| Final circuit -> loads | `Load.CircuitId` + load types | EXISTS | Can show destination/load summary from real data. |
| Board incoming electrical source / empalme | No electrical source entity in current project schema | **MISSING** | Main-board incoming source must initially be Unknown/Not modeled, or be added later as canonical data. |
| Board nominal voltage / phase count | Not stored on `Board` | PARTIAL | May be obtainable from an explicit incoming feeder once topology exists; do not guess for main board. |
| Main board bus as a structural drawing object | No bus entity | PARTIAL | A visual bus may be generated as board grammar, but bus rating/section cannot be claimed unless canonical data is added. |
| Bus current/section/capacity | No canonical fields | MISSING | Omit/show unknown until modeled. |
| Branch breaker adopted | `Circuit.AdoptedBreakerCurrentAmperes`, `AdoptedCurveCode` | EXISTS | Represents adopted branch protection values. |
| Branch breaker recommended | `CircuitResult.Recommended` | EXISTS | Derived/read-only result. |
| Protection chain as independent devices | No protection entity/list | PARTIAL | Current model effectively supports breaker selection, not arbitrary chains. |
| Differential protection (RCD/ID) | No canonical fields/entity | **MISSING** | Do not render an RCD from service type or heuristics. |
| Breaker breaking capacity (Icu/Icn) | Not in current `Circuit` adopted fields | MISSING | Do not invent. |
| Conductor material/type/section | Circuit adopted fields + result recommended/adopted | EXISTS | Choose adopted/current display policy explicitly. |
| Neutral conductor section | `NeutralSectionSquareMillimetres` | PARTIAL | A value proves a section is known; null does not distinguish absent vs unknown/not adopted. |
| Neutral presence | No explicit tri-state field | **MISSING / AMBIGUOUS** | Do not infer solely from MONOFASICO/TRIFASICO or ActiveConductors without a defined product rule. |
| PE presence / PE conductor | No explicit canonical field | **MISSING** | Do not render PE as universally present from the data model alone. |
| Grounding scheme / board grounding | No grounding entity/fields | **MISSING** | Future canonical extension if the diagram must represent it. |
| Circuit active/inactive | `Circuit.State` | EXISTS | Respect host state. |
| Board active/inactive | `Board.State` | EXISTS | Respect host state. |
| Data completeness state | `DataState` | EXISTS | Can surface missing/review/adopted state. |
| Installed power | Loads + load type power / `CircuitResult.TotalPowerW` | EXISTS | Prefer the current read-model policy already used by the host. |
| Theoretical/design current | `CircuitResult` | EXISTS | Derived/read-only. |
| Ampacity / voltage drop | `CircuitResult` | EXISTS | Derived/read-only. |
| Current vs STALE result | `CircuitResult.GlobalStatus` + workspace invalidation | EXISTS | `ProjectWorkspace.Apply` marks affected circuit results `STALE` when supported dependencies change. |
| Calculation execution/trace | executions + calculation values | EXISTS | Secondary detail; not needed to place geometry. |
| Navigation identity | Board/Circuit UIDs | EXISTS | Use semantic references in DiagramScene. |

## Evidence paths

Canonical/source model:

- `src/ProyectoElectrico.V6.Domain/Model/Board.cs`
- `src/ProyectoElectrico.V6.Domain/Model/Circuit.cs`
- `src/ProyectoElectrico.V6.Domain/Model/Load.cs`
- `src/ProyectoElectrico.V6.Application/Projects/ProjectSnapshot.cs`

Read projections and current-result state:

- `src/ProyectoElectrico.V6.Application/Presentation/ProjectReadModels.cs`
- `src/ProyectoElectrico.V6.Application/Presentation/ProjectReadModelBuilder.cs`
- `src/ProyectoElectrico.V6.Application/Projects/ProjectWorkspace.cs`

Persistence contract:

- `docs/architecture/DATA_MODEL.md`
- `docs/architecture/PERSISTENCE.md`

Scale fixture inspected:

- `samples/EdificioOficinasDemo.peproj/boards.csv`
- `samples/EdificioOficinasDemo.peproj/circuits.csv`

The scale fixture confirms the core ambiguity: nine downstream boards declare
`ParentBoardId = 1`, but the eight existing TGBT circuits do not contain a
canonical destination-board FK. Therefore no correct one-to-one mapping from
those child boards to feeder circuits can be recovered from the data without
guessing.

## Important distinction: structural drawing vs electrical fact

Not every line in the drawing needs a persisted domain entity.

Example: a horizontal main-bus line can be a **view grammar element** if all we
claim is "this is the board distribution backbone in the diagram."

But the following are electrical facts and must not be fabricated by the view:

- bus current rating;
- bus section;
- exact feeder circuit feeding another board;
- presence/type/rating of RCD;
- PE conductor data;
- grounding scheme;
- incoming source data.

This distinction lets us keep the host model small without allowing the
renderer to invent engineering information.

## Accepted topology decision

The product requirements are now explicit:

1. A downstream board **may have more than one incoming supply**.
2. A feeder circuit **may feed more than one downstream board**.
3. Generator-specific modeling is future scope, but the topology chosen now
   must not make future normal/emergency or alternate-source support impossible.
4. In the single-line diagram, lines never merge/split merely by touching.
   **Every union or fan-out is concentrated on a bar/bus with explicit node(s).**

Therefore the previously considered shortcut:

```text
Circuit.DownstreamBoardId
```

is rejected. It cannot represent the required cardinalities.

The canonical direction is a separate supply relation in ProyectoElectrico,
conceptually:

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

This permits both:

```text
one feeder circuit -> many downstream boards
one downstream board <- many feeder circuits
```

The exact persistence/schema migration belongs to ProyectoElectrico and must be
implemented there, not hidden inside UI_Unilineal.

### Graphic consequence

When one feeder supplies several boards, BoardDetail derives a distribution
junction bar from the canonical supply relations:

```text
            feeder
              |
        ======●======
           ●     ●
           |     |
        Board A Board B
```

No direct T-junction made from touching polylines is accepted.

The generated junction bar is a **view-grammar element**, not automatically a
persisted electrical entity. It must not invent current rating, section or
capacity. If engineering properties are later attached to that bus, it must
then become canonical in ProyectoElectrico.

See the accepted decision record:

- `docs/integration/R1_TOPOLOGY_DECISION.md`

## Remaining R1 gaps not resolved by this decision

The supply-topology decision does not invent information that ProyectoElectrico
still does not model, including:

- main-board utility/source/empalme entity;
- differential protection;
- explicit neutral-presence state;
- PE conductor/presence;
- grounding scheme;
- bus electrical ratings.

Those remain explicit Unknown/Missing until the canonical host model supports
them.
