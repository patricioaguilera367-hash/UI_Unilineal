# UI_Unilineal V1 G7 — Interaction and Commands Implementation Plan

> **Execution rule:** implement task-by-task with RED -> observed RED -> minimal GREEN -> focused tests -> full regression -> commit. Every task must preserve all G0-G6 evidence.

**Goal:** Add the V1 interaction layer over the closed G6 renderer without moving electrical truth into the renderer. G7 introduces explicit interaction modes, a deterministic state machine, reversible layout commands/history, electrical command proposals and host contracts/capabilities. The renderer translates pointer/keyboard input only; it never executes host electrical mutations.

**Architecture:** Neutral interaction/command contracts live in `UI_Unilineal.Engine` and depend only on Domain. `UI_Unilineal.Rendering.Avalonia` translates Avalonia input and scene hits into neutral interaction events/intents. `UI_Unilineal.Playground` owns demo orchestration, navigation and any fake host used for acceptance. `ProyectoElectrico` is not referenced in G7.

**Spec:** `docs/superpowers/specs/2026-09-16-single-line-v1-design.md` §§14.4, 15, 15.1-15.3, 16, 17 and G7 in §24.

## Global constraints

- Preserve dependency direction `Rendering.Avalonia -> Engine -> Domain`.
- Domain and Engine remain free of Avalonia and `ProyectoElectrico`.
- `SingleLineView` may translate input; it may not execute electrical commands, persist layout, own navigation or own host state.
- `DiagramScene` remains immutable logical geometry. Interaction overlays are not scene elements.
- Dragging in Layout mode can only produce presentation/layout intent.
- Dragging in Navigate mode never produces electrical or layout commands.
- Electrical connection intent can only start in explicit Electrical mode and from a semantic anchor.
- Electrical gestures produce `ElectricalCommandProposal`; no optimistic electrical mutation is permitted.
- Layout and electrical histories are separate.
- Layout history is locally reversible.
- Electrical undo is an inverse host command subject to revision checks; no forced stale rollback.
- Read-only hosts remain useful via `HostCapabilities`.
- G7 does not add SVG/PDF/document composition (G8).
- G7 does not integrate `ProyectoElectrico` (G10/G11).
- Final G7 checkpoint requires Windows and Ubuntu CI GREEN on the exact final SHA, zero warnings/errors, format clean and repository cleanliness green.

---

## File map locked for G7

### Engine interaction contracts

- `src/UI_Unilineal.Engine/Interaction/InteractionMode.cs`
- `src/UI_Unilineal.Engine/Interaction/InteractionState.cs`
- `src/UI_Unilineal.Engine/Interaction/InteractionStateMachine.cs`
- `src/UI_Unilineal.Engine/Interaction/HostCapabilities.cs`
- `src/UI_Unilineal.Engine/Interaction/CommandImpact.cs`

### Engine layout commands

- `src/UI_Unilineal.Engine/Interaction/Layout/ILayoutCommand.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/LayoutCommandResult.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/LayoutCommandReducer.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/LayoutCommandHistory.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/MoveEntityCommand.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/SetEntityLockModeCommand.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/ResetEntityPositionCommand.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/ResetBoardLayoutCommand.cs`
- `src/UI_Unilineal.Engine/Interaction/Layout/ResetAllLayoutCommand.cs`

### Engine electrical commands and host ports

- `src/UI_Unilineal.Engine/Interaction/Electrical/IElectricalCommand.cs`
- `src/UI_Unilineal.Engine/Interaction/Electrical/ElectricalCommandProposal.cs`
- `src/UI_Unilineal.Engine/Interaction/Electrical/ElectricalCommands.cs`
- `src/UI_Unilineal.Engine/Interaction/Electrical/IElectricalCommandHandler.cs`
- `src/UI_Unilineal.Engine/Interaction/Electrical/CommandResult.cs`
- `src/UI_Unilineal.Engine/Interaction/Electrical/CommandResultStatus.cs`

### Rendering.Avalonia interaction translation

- `src/UI_Unilineal.Rendering.Avalonia/Interaction/InteractionInputTranslator.cs`
- `src/UI_Unilineal.Rendering.Avalonia/Interaction/InteractionGestureState.cs`
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/SingleLineView.cs`
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/InteractionOverlayState.cs`
- `src/UI_Unilineal.Rendering.Avalonia/Rendering/InteractionOverlayRenderer.cs`

### Tests

- `tests/UI_Unilineal.Engine.Tests/Interaction/InteractionContractTests.cs`
- `tests/UI_Unilineal.Engine.Tests/Interaction/InteractionStateMachineTests.cs`
- `tests/UI_Unilineal.Engine.Tests/Interaction/LayoutCommandHistoryTests.cs`
- `tests/UI_Unilineal.Engine.Tests/Interaction/ElectricalCommandContractTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Interaction/InteractionInputTranslatorTests.cs`
- `tests/UI_Unilineal.Rendering.Avalonia.Tests/Rendering/SingleLineViewInteractionTests.cs`

### Playground/docs

- `src/UI_Unilineal.Playground/ViewModels/SingleLineWorkspaceViewModel.cs`
- `src/UI_Unilineal.Playground/Views/MainWindow.axaml`
- `src/UI_Unilineal.Playground/Views/MainWindow.axaml.cs`
- `README.md`
- `docs/architecture/README.md`

---

### Task 34: Neutral interaction contracts and host capabilities

**Purpose:** establish the non-Avalonia vocabulary that every later G7 task consumes.

**Required contracts:**

```csharp
public enum InteractionMode
{
    Navigate,
    Layout,
    Electrical
}

public enum InteractionStateKind
{
    Idle,
    Hovering,
    Selecting,
    Panning,
    DraggingLayout,
    ConnectingElectrical,
    MarqueeSelecting,
    CommandPreview
}

public enum CommandImpact
{
    PresentationOnly,
    LocalElectrical,
    CascadingElectrical,
    Destructive
}

public sealed record HostCapabilities(
    bool CanEditLayout,
    bool CanEditElectrical,
    bool CanCreateCircuits,
    bool CanDeleteCircuits,
    bool CanEditProtection,
    bool CanExport,
    bool CanPersistLayout);
```

`HostCapabilities.ReadOnly` must disable every mutation capability while leaving viewing implicit/available.

- [ ] RED: add tests for complete enum members, unique values and read-only capability semantics.
- [ ] RED: architecture test proves Engine interaction contracts contain no Avalonia or ProyectoElectrico references.
- [ ] GREEN: add only the neutral contracts, no gesture logic.
- [ ] Run focused tests + full solution.
- [ ] Commit: `feat(interaction): define neutral G7 interaction contracts`.

---

### Task 35: Deterministic interaction state machine

**Purpose:** replace independent booleans with one explicit stable interaction state.

**Core API:**

```csharp
public sealed record InteractionState(
    InteractionMode Mode,
    InteractionStateKind Kind,
    SceneId? ActiveSceneElementId = null,
    string? ActiveAnchorId = null);

public enum InteractionEventKind
{
    HoverEntered,
    HoverCleared,
    BeginSelection,
    EndSelection,
    BeginPan,
    EndPan,
    BeginLayoutDrag,
    EndLayoutDrag,
    BeginElectricalConnection,
    EndElectricalConnection,
    BeginMarquee,
    EndMarquee,
    ShowCommandPreview,
    AcceptCommandPreview,
    Cancel
}

public sealed class InteractionStateMachine
{
    public InteractionState State { get; }
    public InteractionState SetMode(InteractionMode mode);
    public InteractionState Apply(InteractionEvent interactionEvent);
}
```

**Required invariants:**

- `Cancel` from every cancelable transient state returns to Idle in the same mode.
- mode change cancels transient state before entering the new mode.
- Layout drag can start only in Layout mode.
- Electrical connection can start only in Electrical mode and only with a non-empty anchor id.
- Navigate mode cannot enter DraggingLayout or ConnectingElectrical.
- CommandPreview can only be entered from a completed proposal/command intent, never by hover alone.
- invalid transitions return a typed rejection/result; they do not silently mutate state.

- [ ] RED: exhaustive transition matrix for every mode x begin-gesture combination.
- [ ] RED: Escape/cancel matrix for all transient states.
- [ ] RED: shuffled equivalent event sequences that should converge produce identical state.
- [ ] GREEN: implement pure deterministic state machine.
- [ ] Run full regression.
- [ ] Commit: `feat(interaction): add explicit interaction state machine`.

---

### Task 36: Reversible layout commands and local history

**Purpose:** make layout edits command-based and reversible without touching electrical truth.

**Initial command set:**

- MoveEntity
- SetEntityLockMode (Auto/Pinned/Locked)
- ResetEntityPosition
- ResetBoardLayout
- ResetAllLayout

**Rules:**

- commands operate on `DiagramLayoutState` only;
- command execution returns both new state and an exact inverse command/snapshot needed for undo;
- no-op commands are represented deterministically and do not pollute history;
- Undo/Redo are separate from electrical history;
- executing a new command after Undo clears the redo branch;
- history has no reference to Avalonia or renderer types.

- [ ] RED: MoveEntity creates/updates one override and inverse restores prior state exactly.
- [ ] RED: lock mode commands preserve position.
- [ ] RED: reset commands remove only intended overrides.
- [ ] RED: execute A/B -> undo B -> undo A -> redo A -> redo B round-trips fingerprint/equality.
- [ ] RED: new execute after undo clears redo.
- [ ] GREEN: reducer + history.
- [ ] Commit: `feat(interaction): add reversible layout command history`.

---

### Task 37: Electrical command vocabulary and proposals

**Purpose:** represent electrical intent without executing it in the renderer.

**Initial command concepts:**

- ChangeBoardSupply
- ChangeSupplyCircuit
- ChangeProtection
- ChangeConductor
- ChangeCircuitData
- CreateSupplyConnection
- RemoveSupplyConnection

**Proposal contract includes:**

- proposed command;
- expected input revision;
- source/target semantic entities;
- source/target anchor ids where applicable;
- `CommandImpact`;
- whether confirmation is required;
- deterministic proposal id/fingerprint derived from semantic content, not timestamps.

**Rules:**

- dragging a board is never convertible to an electrical command;
- connection proposal requires compatible semantic anchors;
- cascading/destructive impact requires confirmation;
- proposal is immutable and does not modify `SingleLineInput`.

- [ ] RED: proposal fingerprint is deterministic.
- [ ] RED: cascading/destructive implies confirmation.
- [ ] RED: incompatible anchors reject proposal creation.
- [ ] RED: proposal creation leaves input/projection/scene fingerprints unchanged.
- [ ] GREEN: contracts + proposal factory.
- [ ] Commit: `feat(interaction): model electrical command proposals`.

---

### Task 38: Host electrical command contract, result model and inverse commands

**Purpose:** define safe host-mediated execution without providing a ProyectoElectrico implementation yet.

**Core API:**

```csharp
public interface IElectricalCommandHandler
{
    ValueTask<CommandResult> ExecuteAsync(
        ElectricalCommandRequest request,
        CancellationToken cancellationToken = default);
}

public enum CommandResultStatus
{
    Applied,
    Rejected,
    NeedsConfirmation,
    Conflict,
    Failed
}
```

`CommandResult` must carry, where applicable:

- changed entities;
- invalidated entities;
- diagnostics;
- rebuilt/new `SingleLineInput`;
- new revision;
- inverse electrical command for a future undo request.

**Rules:**

- request carries `ExpectedRevision`;
- Conflict means no canonical mutation is assumed;
- renderer cannot reference or invoke a concrete host handler;
- electrical undo is another host request using the inverse command and current expected revision.

- [ ] RED: all result states have explicit invariants.
- [ ] RED: conflict/rejected/failed cannot expose a falsely applied new input.
- [ ] RED: inverse command round-trip contract is representable.
- [ ] GREEN: port + request/result contracts only.
- [ ] Commit: `feat(interaction): define revision-aware electrical host port`.

---

### Task 39: Avalonia interaction translation and G7 overlays

**Purpose:** translate G6 hits/pointer input into neutral state transitions and intents without command execution.

**Required behavior:**

- Navigate: click/hover/select, pan, viewport gestures only.
- Layout: semantic element drag produces a layout move intent; live drag ghost may be optimistic overlay only.
- Electrical: only semantic anchor press-drag-release can produce an electrical connection intent/proposal request.
- Escape cancels current transient gesture and removes preview/ghost.
- marquee selection is an overlay and produces selection intent only.
- scene fingerprint never changes because of input translation.

**SingleLineView extensions may expose events such as:**

- `SelectionChanged`
- `LayoutMoveRequested`
- `ElectricalProposalRequested`
- `InteractionStateChanged`

The view must not execute `LayoutCommandHistory` or `IElectricalCommandHandler`.

- [ ] RED: same pointer drag under each mode produces different allowed intent family.
- [ ] RED: Layout drag can never raise electrical proposal.
- [ ] RED: Electrical drag from non-anchor raises no proposal.
- [ ] RED: Escape cancels drag/connection/marquee/preview.
- [ ] RED: overlays do not alter scene fingerprint/count.
- [ ] GREEN: input translator + view integration.
- [ ] Commit: `feat(rendering): translate explicit G7 interaction modes`.

---

### Task 40: Playground G7 orchestration

**Purpose:** prove the interaction contracts can be composed without contaminating the renderer.

**Shell responsibilities:**

- mode selector: Navigate / Layout / Electrical;
- selection display;
- layout Undo / Redo;
- optional Pin / Lock / Reset selected layout controls;
- host capability-aware enable/disable state;
- command preview panel for electrical proposals;
- fake/demo handler may return Rejected/NeedsConfirmation/Conflict/Applied for deterministic acceptance fixtures, but must not imitate ProyectoElectrico persistence.

**Layout flow:**

```text
SingleLineView gesture
-> layout intent
-> LayoutCommandHistory.Execute
-> new DiagramLayoutState
-> rebuild scene through SingleLineLayoutEngine
-> assign new immutable scene to SingleLineView
```

**Electrical flow:**

```text
SingleLineView anchor gesture
-> ElectricalCommandProposal
-> command preview
-> optional confirmation
-> IElectricalCommandHandler (demo only in Playground)
-> rebuilt input/projection/scene only if Applied
```

- [ ] RED/pure tests for mode/capability enablement.
- [ ] GREEN shell orchestration with no projection/layout algorithms in code-behind.
- [ ] Manual-runtime acceptance in Playground.
- [ ] Commit: `feat(playground): expose G7 interaction command workflow`.

---

### Task 41: G7 model-based hardening and checkpoint

- [ ] Full restore/format/build/test/diff/cleanliness run.
- [ ] Explicit architecture tests.
- [ ] Exhaustive small transition matrix for modes/states/events.
- [ ] Model-based sequence test for layout execute/undo/redo/cancel.
- [ ] Electrical rejected/conflict result proves no optimistic diagram mutation.
- [ ] Read-only capability fixture proves viewing/navigation/selection still work while edit intents are disabled.
- [ ] Self-review against spec §§14.4, 15, 16, 17.
- [ ] Update README and architecture docs.
- [ ] Commit: `docs: mark V1 G7 interaction checkpoint`.
- [ ] Verify exact final SHA CI: Windows GREEN + Ubuntu GREEN.
- [ ] Optional tag desired: `g7-green-2026-09-19`.

---

## G7 acceptance boundary

G7 is GREEN only when all of the following are simultaneously true:

1. Navigate/Layout/Electrical are explicit and mutually exclusive modes.
2. Interaction is represented by one deterministic state machine.
3. Layout edits are commands with local undo/redo and cannot mutate electrical topology.
4. Electrical gestures yield immutable proposals, not renderer-side mutations.
5. Host capabilities gate unavailable edits without disabling read-only use.
6. Host electrical execution is represented by a revision-aware port and typed result model.
7. Electrical undo is representable as a current-revision inverse host command.
8. Renderer only translates input and renders overlays; shell/host own execution and navigation.
9. G0-G6 regression evidence remains green.
10. No G8 exporter/document code and no ProyectoElectrico integration enter this gate.

## Sequencing rationale

Tasks 34-38 deliberately build pure, neutral contracts before any Avalonia gesture work. This keeps state/undo/electrical safety testable without UI timing or pointer APIs. Task 39 then maps real renderer input onto those contracts. Task 40 proves composition in the Playground. Task 41 closes the gate with sequence-level evidence instead of relying only on individual unit tests.
