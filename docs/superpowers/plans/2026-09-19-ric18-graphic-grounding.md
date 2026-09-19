# UI_Unilineal V1 — RIC18 Graphic Grounding

Date: 2026-09-19  
Branch: `feature/ric18-graphic-grounding`  
Base: G8 checkpoint `3766d87e44ee0eb4733f1c37972f0c3b953364a2`

## Purpose

Turn the existing renderer-neutral drawing profile into a visually reviewable, traceable RIC 18-oriented graphic system without inventing regulatory requirements.

This work is intentionally incremental. Each task must end in a small reviewable commit so the owner can pull, run the Playground, inspect the result visually, and approve or request corrections before the next task.

## Non-negotiable rules

1. RIC source images are references, not runtime bitmap assets.
2. Runtime symbols remain renderer-neutral vector definitions.
3. Shape provenance and size provenance are independent.
4. If RIC 18 shows a shape but does not define exact physical dimensions, the shape may be `RIC18_REFERENCE` while its size/spacing remains `APP_CONVENTION`.
5. No OCR-derived or AI-inferred geometry is promoted to normative provenance without explicit human verification.
6. Existing G0–G8 architecture remains intact; this work must not move electrical semantics into the renderer.
7. Every symbol must have deterministic nominal bounds, anchors, primitives and label slots.
8. Symbols in the same functional family must share explicit sizing/alignment rules.
9. Visual inspection supplements automated tests; it does not replace them.
10. Each commit must be independently pullable and reviewable.

## Review loop

For every task:

1. RED/fixture or visual target is defined.
2. One small implementation is committed.
3. CI/tests are checked.
4. The owner pulls the branch and opens the Playground.
5. The owner approves or reports visual changes.
6. Only then do we continue to the next symbol/family.

## Task sequence

### Task R1 — Symbol Gallery harness

Create a dedicated Playground view that renders every symbol from the active drawing profile on a shared millimetre grid, with:
- symbol id and semantic role;
- nominal bounding box;
- visible anchors;
- family grouping;
- same zoom/scale for all symbols;
- optional baseline/centre guides.

No RIC geometry changes in this task.

**Exit:** existing APP_CONVENTION symbols can be inspected together in one view.

### Task R2 — Graphic conventions contract

Introduce an explicit graphic-conventions model/data file for:
- base protection cell size;
- source/load cell size;
- anchor alignment;
- standard stroke classes;
- label clearances;
- symbol family rules.

Values without authoritative RIC support remain `APP_CONVENTION`.

**Exit:** symbol proportions are no longer independent magic numbers.

### Task R3 — Provenance grounding for Annex 18.5

Add provenance records for the provided RIC N°18 Annex 18.5 reference figure. Do not automatically reclassify every existing symbol.

Create a mapping table:
- semantic symbol;
- visible analogue in Annex 18.5;
- confidence/state: VERIFIED / PARTIAL / NOT_SHOWN / AMBIGUOUS;
- shape provenance;
- size provenance.

**Exit:** the repo explicitly knows which graphical claims come from Annex 18.5 and which remain application conventions.

### Task R4 — Protection family

Rebuild and visually validate, one at a time:
- breaker;
- residual-current device;
- fuse if supported by verified source/reference.

Use shared family dimensions/anchors. Each symbol gets its own reviewable commit if visual changes are material.

**Exit:** protection symbols are visually coherent and traceable.

### Task R5 — Distribution and grounding family

Validate/rebuild:
- bus;
- connection node;
- grounding;
- incoming/service element where supported.

**Exit:** distribution backbone matches the chosen RIC-oriented graphic language.

### Task R6 — Loads and downstream-board family

Validate/rebuild:
- final load(s);
- downstream board;
- unknown endpoint conventions.

Where Annex 18.5 only shows circuit numbering/service text and not a universal load symbol, retain application convention rather than claiming RIC authority.

### Task R7 — Annex 18.5 board composition fixture

Create a canonical demo fixture inspired by Annex 18.5:
- incoming supply;
- main protection;
- main bus;
- grounding;
- four outgoing circuits;
- protection chains;
- labels/data zones.

The fixture is a regression/reference composition, not a claim that every project must use that exact arrangement.

**Exit:** owner can visually compare a generated board against the RIC example.

### Task R8 — Annex 18.1 paper and margins

Encode and test verified project-presentation formats:
- A0–A4 existing dimensions;
- 2A0 and 4A0 where supported by the source;
- RIC project margins from Annex 18.1 as named policy presets, separate from generic configurable margins.

**Exit:** document export can explicitly select a RIC18 presentation policy.

### Task R9 — Graphic checkpoint

Run accumulated tests/CI, capture gallery/demo evidence and document:
- verified RIC references;
- remaining APP_CONVENTION items;
- unresolved ambiguities;
- approved visual baseline.

Only after this checkpoint should host integration continue.

## First implementation target

Start with **R1 only**. Do not change symbol geometry yet. The first thing the owner should see is a trustworthy gallery of the symbols that already exist.
