# Single-Line V1 Core Semantic Pipeline (G0–G2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first production slice of UI_Unilineal V1: a reproducible CI baseline, the immutable semantic `SingleLineInput` contract with structural validation, and deterministic summary/detail `SingleLineProjection` output with regression evidence.

**Architecture:** `UI_Unilineal.Domain` owns only immutable semantic contracts and identity/state types. `UI_Unilineal.Engine` owns validation, fingerprints and projection; it references Domain and nothing UI- or host-specific. G0–G2 stop before drawing profiles, geometry, layout and Avalonia rendering: the deliverable is `SingleLineInput -> validation -> SingleLineProjection` with deterministic ordering and explicit diagnostics.

**Tech Stack:** .NET 9.0 / SDK 9.0.200 with `rollForward=latestFeature`, C# `latest`, xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0, coverlet.collector 6.0.2, GitHub Actions, `System.Text.Json`, `System.Security.Cryptography`.

**Spec:** `docs/superpowers/specs/2026-09-16-single-line-v1-design.md`

## Global Constraints

- Production branch: `feature/v1-single-line-engine`, created from clean `main`; V0.x spike branches are evidence only and no production code is copied from them.
- Target framework remains `net9.0`; SDK floor remains `9.0.200` with `rollForward=latestFeature` and `allowPrerelease=false`.
- `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`, `Deterministic=true`, `TreatWarningsAsErrors=true` remain enabled.
- `UI_Unilineal.Domain` must not reference Avalonia, ProyectoElectrico, persistence, CSV, `.peproj`, Desktop or Infrastructure types.
- `UI_Unilineal.Engine` may reference `UI_Unilineal.Domain`; it must not reference Avalonia or ProyectoElectrico.
- `SingleLineInput` is immutable from the consumer's point of view; caller-owned mutable collections must be defensively copied.
- Missing data is represented as `null`/explicit state, never converted to numeric zero.
- Input validation is strict on referential/structural integrity and tolerant on incomplete electrical values.
- Every G2 projection must be deterministic for semantically equivalent input regardless of source collection order.
- No host IDs (`long BoardId`, `long CircuitId`) appear in UI_Unilineal contracts; identity uses `EntityUid` and `EntityReference`.
- G0–G2 do not introduce RIC symbol claims, layout coordinates, renderer types, `.peproj` integration, command execution or export code.
- Every production behavior is implemented test-first. A confirmed bug discovered during execution receives a failing regression test before the fix.
- Each task finishes with all previously accepted tests green and a small coherent commit.

---

## File Structure Locked by This Plan

The following new files are the intended G0–G2 structure. Keep one clear responsibility per file rather than merging these into large generic model/utility files.

```text
.github/workflows/
  ci.yml

src/UI_Unilineal.Domain/Semantics/
  EntityUid.cs
  EntityKind.cs
  EntityReference.cs
  SemanticStates.cs
  SemanticKinds.cs
  ProjectInput.cs
  SourceInput.cs
  BoardInput.cs
  ConductorInput.cs
  CircuitInput.cs
  BusInput.cs
  SupplyConnection.cs
  ProtectionInput.cs
  GroundingInput.cs
  ElectricalResultInput.cs
  SingleLineInputMetadata.cs
  SingleLineInput.cs

src/UI_Unilineal.Engine/Validation/
  ValidationSeverity.cs
  ValidationIssue.cs
  ValidationCodes.cs
  InputValidationResult.cs
  SemanticEntityIndex.cs
  SupplyTopologyValidator.cs
  CompletenessValidator.cs
  SingleLineInputValidator.cs

src/UI_Unilineal.Engine/Projection/
  ProjectionStatus.cs
  ProjectionIssue.cs
  SummaryProjection.cs
  BoardDetailProjection.cs
  SingleLineProjection.cs
  ProjectionBuildResult.cs
  ProjectionStatusResolver.cs
  SingleLineInputFingerprint.cs
  SummaryProjectionBuilder.cs
  BoardDetailProjectionBuilder.cs
  SingleLineProjectionBuilder.cs

tests/UI_Unilineal.Domain.Tests/Semantics/
  EntityUidTests.cs
  SingleLineInputTests.cs

tests/UI_Unilineal.Engine.Tests/Architecture/
  DependencyBoundaryTests.cs

tests/UI_Unilineal.Engine.Tests/Fixtures/
  SemanticFixtureFactory.cs

tests/UI_Unilineal.Engine.Tests/Validation/
  SingleLineInputValidatorTests.cs
  GeneratedTopologyTests.cs

tests/UI_Unilineal.Engine.Tests/Projection/
  SingleLineInputFingerprintTests.cs
  ProjectionStatusResolverTests.cs
  SummaryProjectionBuilderTests.cs
  BoardDetailProjectionBuilderTests.cs
  SingleLineProjectionBuilderTests.cs
  ProjectionGoldenFormatter.cs
  ProjectionGoldenTests.cs

tests/UI_Unilineal.Engine.Tests/Golden/Projection/
  minimal-project.txt
  nested-boards.txt
```

Do not create `Scene`, `Profiles`, `Composition`, `Layout`, `Rendering` production classes in this plan; those begin with G3.

---

### Task 1: G0 baseline, architecture boundary checks and CI

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `tests/UI_Unilineal.Engine.Tests/Architecture/DependencyBoundaryTests.cs`
- Modify: none of the production `.csproj` files

**Interfaces:**
- Consumes: existing `UI_Unilineal.sln`, `Domain.AssemblyMarker`, `Engine.AssemblyMarker`.
- Produces: executable architecture boundary tests and a two-OS CI gate used by every later task.

- [ ] **Step 1: Run the untouched branch baseline**

Run:

```bash
dotnet --version
dotnet restore UI_Unilineal.sln
dotnet build UI_Unilineal.sln -c Release --no-restore
dotnet test UI_Unilineal.sln -c Release --no-build --logger "console;verbosity=normal"
```

Expected: SDK resolves from `global.json`; restore/build/test all exit `0`; build emits zero warnings because warnings are errors.

- [ ] **Step 2: Write architecture boundary tests**

Create `DependencyBoundaryTests.cs`:

```csharp
namespace UI_Unilineal.Engine.Tests.Architecture;

public sealed class DependencyBoundaryTests
{
    [Fact]
    public void Domain_DoesNotReferenceUiOrHostAssemblies()
    {
        string[] references = typeof(Domain.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(references, x => x.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.StartsWith("ProyectoElectrico", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.Contains("Infrastructure.Csv", StringComparison.Ordinal));
    }

    [Fact]
    public void Engine_ReferencesDomainButNotUiOrHostAssemblies()
    {
        string[] references = typeof(Engine.AssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(x => x.Name ?? string.Empty)
            .ToArray();

        Assert.Contains("UI_Unilineal.Domain", references);
        Assert.DoesNotContain(references, x => x.StartsWith("Avalonia", StringComparison.Ordinal));
        Assert.DoesNotContain(references, x => x.StartsWith("ProyectoElectrico", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 3: Run the boundary tests**

Run:

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~DependencyBoundaryTests
```

Expected: PASS.

- [ ] **Step 4: Add GitHub Actions CI**

Create `.github/workflows/ci.yml` exactly with these gates:

```yaml
name: ci

on:
  push:
    branches:
      - main
      - feature/v1-single-line-engine
  pull_request:

jobs:
  build-test:
    strategy:
      fail-fast: false
      matrix:
        os:
          - ubuntu-latest
          - windows-latest
    runs-on: ${{ matrix.os }}

    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Restore
        run: dotnet restore UI_Unilineal.sln

      - name: Format check
        run: dotnet format UI_Unilineal.sln --verify-no-changes --no-restore

      - name: Build Release
        run: dotnet build UI_Unilineal.sln -c Release --no-restore

      - name: Test Release
        run: dotnet test UI_Unilineal.sln -c Release --no-build --logger "console;verbosity=normal"
```

- [ ] **Step 5: Verify locally that CI commands remain green**

Run:

```bash
dotnet restore UI_Unilineal.sln
dotnet format UI_Unilineal.sln --verify-no-changes --no-restore
dotnet build UI_Unilineal.sln -c Release --no-restore
dotnet test UI_Unilineal.sln -c Release --no-build
```

Expected: all commands exit `0`.

- [ ] **Step 6: Commit G0 infrastructure**

```bash
git add .github/workflows/ci.yml tests/UI_Unilineal.Engine.Tests/Architecture/DependencyBoundaryTests.cs
git commit -m "ci: establish V1 baseline gates"
```

---

### Task 2: Stable semantic identity and state enums

**Files:**
- Create: `src/UI_Unilineal.Domain/Semantics/EntityUid.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/EntityKind.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/EntityReference.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/SemanticStates.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/SemanticKinds.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Semantics/EntityUidTests.cs`

**Interfaces:**
- Produces: `EntityUid`, `EntityReference`, identity/state/role enums consumed by every later semantic record and projection type.

- [ ] **Step 1: Write failing identity tests**

Create `EntityUidTests.cs`:

```csharp
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Semantics;

public sealed class EntityUidTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankValues(string value)
    {
        Assert.Throws<ArgumentException>(() => new EntityUid(value));
    }

    [Fact]
    public void Equality_IsOrdinalAndValueBased()
    {
        Assert.Equal(new EntityUid("BOARD:A"), new EntityUid("BOARD:A"));
        Assert.NotEqual(new EntityUid("BOARD:A"), new EntityUid("board:a"));
    }

    [Fact]
    public void EntityReference_KeepsKindSeparateFromUid()
    {
        var uid = new EntityUid("A");

        Assert.NotEqual(
            new EntityReference(uid, EntityKind.Board),
            new EntityReference(uid, EntityKind.Circuit));
    }
}
```

- [ ] **Step 2: Run the test and verify RED**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release --filter FullyQualifiedName~EntityUidTests
```

Expected: compile failure because the semantic identity types do not exist.

- [ ] **Step 3: Implement identity types**

`EntityUid.cs`:

```csharp
namespace UI_Unilineal.Domain.Semantics;

public sealed record EntityUid
{
    public EntityUid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Entity UID cannot be blank.", nameof(value));

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
```

`EntityKind.cs`:

```csharp
namespace UI_Unilineal.Domain.Semantics;

public enum EntityKind
{
    Project,
    Source,
    Board,
    Circuit,
    SupplyConnection,
    Protection,
    Bus,
    Grounding,
    Load
}
```

`EntityReference.cs`:

```csharp
namespace UI_Unilineal.Domain.Semantics;

public sealed record EntityReference(EntityUid Uid, EntityKind Kind)
{
    public EntityUid Uid { get; init; } = Uid ?? throw new ArgumentNullException(nameof(Uid));
}
```

`SemanticStates.cs`:

```csharp
namespace UI_Unilineal.Domain.Semantics;

public enum OperationalState { Active, Inactive, Unknown }
public enum DataState { Complete, Incomplete, Invalid, Unknown }
public enum ResultState { Current, Stale, Pending, Missing, Unknown }
```

`SemanticKinds.cs`:

```csharp
namespace UI_Unilineal.Domain.Semantics;

public enum SourceKind { Utility, Transformer, Generator, Battery, Ups, Alternative, Unknown }
public enum BoardRole { Main, Distribution, SubDistribution, Control, Emergency, Other, Unknown }
public enum CircuitRole { Final, Feeder, Subfeeder, Reserved, Other, Unknown }
public enum BusRole { Main, Section, Other }
public enum SupplyRole { Normal, Emergency, Alternative, Bypass, TransferInput, Unknown }
public enum ProtectionKind { Breaker, Differential, Fuse, Combined, Other, Unknown }
public enum ProtectionRole { Main, Branch, Feeder, Backup, Recommended, Adopted, Other }
public enum GroundingKind { Protection, Service, Functional, Combined, Unknown }
```

- [ ] **Step 4: Run Domain tests and verify GREEN**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release
```

Expected: all Domain tests pass.

- [ ] **Step 5: Commit semantic identity**

```bash
git add src/UI_Unilineal.Domain/Semantics tests/UI_Unilineal.Domain.Tests/Semantics/EntityUidTests.cs
git commit -m "feat(domain): add stable semantic identity"
```

---

### Task 3: Core project, source, board, circuit and conductor contracts

**Files:**
- Create: `src/UI_Unilineal.Domain/Semantics/ProjectInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/SourceInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/BoardInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/ConductorInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/CircuitInput.cs`
- Test: `tests/UI_Unilineal.Domain.Tests/Semantics/SingleLineInputTests.cs` initially containing record-contract tests

**Interfaces:**
- Consumes: Task 2 identity/state enums.
- Produces: immutable entity facts consumed by topology, validation and projection.

- [ ] **Step 1: Write failing contract tests**

Add to `SingleLineInputTests.cs`:

```csharp
using UI_Unilineal.Domain.Semantics;

namespace UI_Unilineal.Domain.Tests.Semantics;

public sealed partial class SingleLineInputTests
{
    [Fact]
    public void CircuitIdentity_IsIndependentFromHumanReadableCode()
    {
        var boardUid = new EntityUid("BOARD-1");
        var circuitUid = new EntityUid("CIRCUIT-1");
        var circuit = new CircuitInput(
            circuitUid, boardUid, 1, "C01", "Alumbrado", CircuitRole.Final,
            "ALUMBRADO", "1F", 230m, 1m, 10m, null, null,
            new ConductorInput("CU", "THHN", 2.5m, 2.5m, 2, null),
            OperationalState.Active, DataState.Complete);

        Assert.Equal(circuitUid, circuit.Uid);
        Assert.Equal("C01", circuit.Code);
    }

    [Fact]
    public void MissingElectricalValues_RemainNull()
    {
        var source = new SourceInput(
            new EntityUid("SOURCE-1"), "S1", "Fuente", SourceKind.Utility,
            null, null, null, null, OperationalState.Active, DataState.Incomplete);

        Assert.Null(source.NominalVoltageV);
        Assert.Null(source.PhaseCount);
        Assert.Null(source.NeutralAvailable);
    }
}
```

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release --filter FullyQualifiedName~SingleLineInputTests
```

Expected: compile failure for missing records.

- [ ] **Step 3: Implement the records with exact signatures**

```csharp
// ProjectInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record ProjectInput(EntityUid Uid, string Code, string Name, OperationalState State);

// SourceInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record SourceInput(
    EntityUid Uid,
    string Code,
    string Name,
    SourceKind Kind,
    string? SystemCode,
    decimal? NominalVoltageV,
    int? PhaseCount,
    bool? NeutralAvailable,
    OperationalState State,
    DataState DataState);

// BoardInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record BoardInput(
    EntityUid Uid,
    int Number,
    string Code,
    string Name,
    BoardRole Role,
    string? Location,
    decimal? NominalVoltageV,
    int? PhaseCount,
    OperationalState State,
    DataState DataState);

// ConductorInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record ConductorInput(
    string? MaterialCode,
    string? TypeCode,
    decimal? PhaseSectionMm2,
    decimal? NeutralSectionMm2,
    int? ActiveConductors,
    string? Description);

// CircuitInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record CircuitInput(
    EntityUid Uid,
    EntityUid BoardUid,
    int Number,
    string Code,
    string Name,
    CircuitRole Role,
    string? ServiceCode,
    string? SystemCode,
    decimal? VoltageV,
    decimal? PowerFactor,
    decimal? LengthM,
    string? RacewayCode,
    string? InstallationMethodCode,
    ConductorInput? Conductor,
    OperationalState State,
    DataState DataState);
```

- [ ] **Step 4: Run Domain tests**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release
```

Expected: PASS.

- [ ] **Step 5: Commit core semantic records**

```bash
git add src/UI_Unilineal.Domain/Semantics tests/UI_Unilineal.Domain.Tests/Semantics/SingleLineInputTests.cs
git commit -m "feat(domain): add project board and circuit inputs"
```

---

### Task 4: Supply, bus, protection, grounding and result contracts

**Files:**
- Create: `src/UI_Unilineal.Domain/Semantics/BusInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/SupplyConnection.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/ProtectionInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/GroundingInput.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/ElectricalResultInput.cs`
- Modify/Test: `tests/UI_Unilineal.Domain.Tests/Semantics/SingleLineInputTests.cs`

**Interfaces:**
- Produces: explicit board-supply topology and independent protection/grounding/result entities.
- Important invariant deferred to Engine validation: a board-origin supply requires a through circuit owned by that board; a source-origin supply has no through board circuit in G1.

- [ ] **Step 1: Write failing topology-contract tests**

Append:

```csharp
[Fact]
public void SupplyConnection_RepresentsBoardCircuitDestinationExplicitly()
{
    var board = new EntityReference(new EntityUid("B1"), EntityKind.Board);
    var supply = new SupplyConnection(
        new EntityUid("SC1"), board, new EntityUid("C4"), new EntityUid("B2"),
        SupplyRole.Normal, 0, true, OperationalState.Active, DataState.Complete);

    Assert.Equal(EntityKind.Board, supply.Origin.Kind);
    Assert.Equal(new EntityUid("C4"), supply.ThroughCircuitUid);
    Assert.Equal(new EntityUid("B2"), supply.DestinationBoardUid);
}

[Fact]
public void Protection_HasSeparateOwnerAndProtectedEntity()
{
    var owner = new EntityReference(new EntityUid("B1"), EntityKind.Board);
    var protects = new EntityReference(new EntityUid("BUS:B1:MAIN"), EntityKind.Bus);
    var protection = new ProtectionInput(
        new EntityUid("P1"), owner, protects, ProtectionKind.Breaker, ProtectionRole.Main,
        3, 40m, 6m, "C", null, null, null, null,
        OperationalState.Active, DataState.Complete);

    Assert.NotEqual(protection.Owner, protection.Protects);
}
```

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release --filter FullyQualifiedName~SingleLineInputTests
```

- [ ] **Step 3: Implement exact record signatures**

```csharp
// BusInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record BusInput(
    EntityUid Uid,
    EntityUid BoardUid,
    string Code,
    BusRole Role,
    decimal? RatedCurrentA,
    OperationalState State,
    DataState DataState);

// SupplyConnection.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record SupplyConnection(
    EntityUid Uid,
    EntityReference Origin,
    EntityUid? ThroughCircuitUid,
    EntityUid DestinationBoardUid,
    SupplyRole Role,
    int Priority,
    bool IsNormallyActive,
    OperationalState State,
    DataState DataState);

// ProtectionInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record ProtectionInput(
    EntityUid Uid,
    EntityReference Owner,
    EntityReference Protects,
    ProtectionKind Kind,
    ProtectionRole Role,
    int? Poles,
    decimal? RatedCurrentA,
    decimal? BreakingCapacityKa,
    string? Curve,
    decimal? DifferentialCurrentMa,
    string? DifferentialType,
    string? Manufacturer,
    string? Model,
    OperationalState State,
    DataState DataState);

// GroundingInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record GroundingInput(
    EntityUid Uid,
    EntityReference Owner,
    GroundingKind Kind,
    string? ConductorMaterialCode,
    decimal? ConductorSectionMm2,
    decimal? ResistanceOhm,
    string? MeasurementMethod,
    string? Instrument,
    OperationalState State,
    DataState DataState);

// ElectricalResultInput.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record ElectricalResultInput(
    EntityReference Entity,
    decimal? InstalledPowerW,
    decimal? TheoreticalCurrentA,
    decimal? DesignCurrentA,
    decimal? CorrectedAmpacityA,
    decimal? VoltageDropV,
    decimal? VoltageDropPercent,
    string? GlobalStatus,
    ResultState ResultState,
    string? ExecutionReference);
```

- [ ] **Step 4: Run Domain tests and format check**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release
dotnet format UI_Unilineal.sln --verify-no-changes
```

Expected: PASS / no formatting changes required after formatting once if needed.

- [ ] **Step 5: Commit explicit electrical contracts**

```bash
git add src/UI_Unilineal.Domain/Semantics tests/UI_Unilineal.Domain.Tests/Semantics/SingleLineInputTests.cs
git commit -m "feat(domain): add explicit supply and electrical entities"
```

---

### Task 5: Immutable `SingleLineInput` aggregate

**Files:**
- Create: `src/UI_Unilineal.Domain/Semantics/SingleLineInputMetadata.cs`
- Create: `src/UI_Unilineal.Domain/Semantics/SingleLineInput.cs`
- Modify/Test: `tests/UI_Unilineal.Domain.Tests/Semantics/SingleLineInputTests.cs`

**Interfaces:**
- Produces: `SingleLineInput` consumed by every Engine service.
- Constructor signature is frozen for G0–G2 and defensively copies every incoming collection.

- [ ] **Step 1: Write a failing defensive-copy test**

```csharp
[Fact]
public void Aggregate_DefensivelyCopiesCallerCollections()
{
    var boards = new List<BoardInput>
    {
        new(new EntityUid("B1"), 1, "TGBT", "TGBT", BoardRole.Main,
            null, 400m, 3, OperationalState.Active, DataState.Complete)
    };

    var input = new SingleLineInput(
        new ProjectInput(new EntityUid("P1"), "P1", "Project", OperationalState.Active),
        [], boards, [], [], [], [], [], [],
        new SingleLineInputMetadata("1", null, null));

    boards.Clear();

    Assert.Single(input.Boards);
}
```

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release --filter FullyQualifiedName~Aggregate_DefensivelyCopiesCallerCollections
```

- [ ] **Step 3: Implement metadata and aggregate**

```csharp
// SingleLineInputMetadata.cs
namespace UI_Unilineal.Domain.Semantics;
public sealed record SingleLineInputMetadata(
    string SchemaVersion,
    string? SourceSystem,
    string? SourceReference);
```

`SingleLineInput.cs` must expose these exact properties:

```csharp
namespace UI_Unilineal.Domain.Semantics;

public sealed class SingleLineInput
{
    public SingleLineInput(
        ProjectInput project,
        IEnumerable<SourceInput> sources,
        IEnumerable<BoardInput> boards,
        IEnumerable<BusInput> buses,
        IEnumerable<CircuitInput> circuits,
        IEnumerable<SupplyConnection> supplyConnections,
        IEnumerable<ProtectionInput> protections,
        IEnumerable<GroundingInput> grounding,
        IEnumerable<ElectricalResultInput> results,
        SingleLineInputMetadata metadata)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Sources = Copy(sources, nameof(sources));
        Boards = Copy(boards, nameof(boards));
        Buses = Copy(buses, nameof(buses));
        Circuits = Copy(circuits, nameof(circuits));
        SupplyConnections = Copy(supplyConnections, nameof(supplyConnections));
        Protections = Copy(protections, nameof(protections));
        Grounding = Copy(grounding, nameof(grounding));
        Results = Copy(results, nameof(results));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public ProjectInput Project { get; }
    public IReadOnlyList<SourceInput> Sources { get; }
    public IReadOnlyList<BoardInput> Boards { get; }
    public IReadOnlyList<BusInput> Buses { get; }
    public IReadOnlyList<CircuitInput> Circuits { get; }
    public IReadOnlyList<SupplyConnection> SupplyConnections { get; }
    public IReadOnlyList<ProtectionInput> Protections { get; }
    public IReadOnlyList<GroundingInput> Grounding { get; }
    public IReadOnlyList<ElectricalResultInput> Results { get; }
    public SingleLineInputMetadata Metadata { get; }

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        return Array.AsReadOnly(values.ToArray());
    }
}
```

- [ ] **Step 4: Run all Domain tests**

```bash
dotnet test tests/UI_Unilineal.Domain.Tests/UI_Unilineal.Domain.Tests.csproj -c Release
```

- [ ] **Step 5: Commit the aggregate**

```bash
git add src/UI_Unilineal.Domain/Semantics tests/UI_Unilineal.Domain.Tests/Semantics/SingleLineInputTests.cs
git commit -m "feat(domain): add immutable single-line input aggregate"
```

---

### Task 6: Validation result model and entity index

**Files:**
- Create: `src/UI_Unilineal.Engine/Validation/ValidationSeverity.cs`
- Create: `src/UI_Unilineal.Engine/Validation/ValidationIssue.cs`
- Create: `src/UI_Unilineal.Engine/Validation/ValidationCodes.cs`
- Create: `src/UI_Unilineal.Engine/Validation/InputValidationResult.cs`
- Create: `src/UI_Unilineal.Engine/Validation/SemanticEntityIndex.cs`
- Create: `tests/UI_Unilineal.Engine.Tests/Fixtures/SemanticFixtureFactory.cs`
- Create: `tests/UI_Unilineal.Engine.Tests/Validation/SingleLineInputValidatorTests.cs`

**Interfaces:**
- Produces: typed validation diagnostics and a private/internal index used by all validation/projection builders.

- [ ] **Step 1: Create a minimal canonical fixture factory**

`SemanticFixtureFactory.Minimal()` must build one complete project with IDs `P1`, `S1`, `B1`, `C1`, `SC1`, `PR1`; source `EMPALME`, board `TGBT`, final circuit `C01`, one breaker and one current result. Use no explicit bus so later projection can prove deterministic bus synthesis.

Use this constructor body as the canonical minimal fixture:

```csharp
public static SingleLineInput Minimal()
{
    var project = new ProjectInput(new EntityUid("P1"), "P1", "Proyecto mínimo", OperationalState.Active);
    var source = new SourceInput(new EntityUid("S1"), "EMPALME", "Empalme", SourceKind.Utility,
        "3F", 400m, 3, true, OperationalState.Active, DataState.Complete);
    var board = new BoardInput(new EntityUid("B1"), 1, "TGBT", "Tablero general", BoardRole.Main,
        "Sala eléctrica", 400m, 3, OperationalState.Active, DataState.Complete);
    var circuit = new CircuitInput(new EntityUid("C1"), board.Uid, 1, "C01", "Alumbrado", CircuitRole.Final,
        "ALUMBRADO", "1F", 230m, 1m, 10m, null, null,
        new ConductorInput("CU", "THHN", 2.5m, 2.5m, 2, null),
        OperationalState.Active, DataState.Complete);
    var supply = new SupplyConnection(new EntityUid("SC1"),
        new EntityReference(source.Uid, EntityKind.Source), null, board.Uid,
        SupplyRole.Normal, 0, true, OperationalState.Active, DataState.Complete);
    var protection = new ProtectionInput(new EntityUid("PR1"),
        new EntityReference(circuit.Uid, EntityKind.Circuit),
        new EntityReference(circuit.Uid, EntityKind.Circuit),
        ProtectionKind.Breaker, ProtectionRole.Branch, 2, 10m, 6m, "C",
        null, null, null, null, OperationalState.Active, DataState.Complete);
    var result = new ElectricalResultInput(
        new EntityReference(circuit.Uid, EntityKind.Circuit), 1800m, 7.83m, 8.61m,
        20m, 2m, 0.87m, "OK", ResultState.Current, "EXEC-1");

    return new SingleLineInput(project, [source], [board], [], [circuit], [supply], [protection], [], [result],
        new SingleLineInputMetadata("1", "TEST", "minimal"));
}
```

- [ ] **Step 2: Write failing duplicate/reference diagnostic tests against the intended validator API**

```csharp
[Fact]
public void Validate_DuplicateBoardUid_ReturnsError()
{
    SingleLineInput source = SemanticFixtureFactory.Minimal();
    BoardInput board = source.Boards[0];
    var invalid = new SingleLineInput(source.Project, source.Sources, [board, board], source.Buses,
        source.Circuits, source.SupplyConnections, source.Protections, source.Grounding, source.Results, source.Metadata);

    InputValidationResult result = new SingleLineInputValidator().Validate(invalid);

    Assert.Contains(result.Issues, x => x.Code == ValidationCodes.DuplicateEntityUid && x.Severity == ValidationSeverity.Error);
}
```

- [ ] **Step 3: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~SingleLineInputValidatorTests
```

- [ ] **Step 4: Implement diagnostic contracts**

Use:

```csharp
public enum ValidationSeverity { Error, Warning }

public sealed record ValidationIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    EntityReference? Entity = null,
    string? Field = null);

public sealed class InputValidationResult
{
    public InputValidationResult(IEnumerable<ValidationIssue> issues) =>
        Issues = Array.AsReadOnly(issues.ToArray());

    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool HasErrors => Issues.Any(x => x.Severity == ValidationSeverity.Error);
}
```

`ValidationCodes` must define constants used by G1–G2:

```csharp
public static class ValidationCodes
{
    public const string DuplicateEntityUid = "DUPLICATE_ENTITY_UID";
    public const string MissingReference = "MISSING_REFERENCE";
    public const string InvalidSupplyOrigin = "INVALID_SUPPLY_ORIGIN";
    public const string BoardSupplyRequiresCircuit = "BOARD_SUPPLY_REQUIRES_CIRCUIT";
    public const string SourceSupplyCannotUseBoardCircuit = "SOURCE_SUPPLY_CANNOT_USE_BOARD_CIRCUIT";
    public const string SupplyCircuitOwnerMismatch = "SUPPLY_CIRCUIT_OWNER_MISMATCH";
    public const string SelfSupply = "SELF_SUPPLY";
    public const string SupplyCycle = "SUPPLY_CYCLE";
    public const string MultipleMainBuses = "MULTIPLE_MAIN_BUSES";
    public const string BoardWithoutSupply = "BOARD_WITHOUT_SUPPLY";
    public const string CircuitMissingConductor = "CIRCUIT_MISSING_CONDUCTOR";
    public const string CircuitMissingProtection = "CIRCUIT_MISSING_PROTECTION";
    public const string BoardMissingVoltage = "BOARD_MISSING_VOLTAGE";
    public const string SourceIncomplete = "SOURCE_INCOMPLETE";
    public const string GroundingIncomplete = "GROUNDING_INCOMPLETE";
}
```

- [ ] **Step 5: Implement `SemanticEntityIndex` only after tests require it**

The internal index must expose `Exists(EntityReference)`, `TryGetBoard`, `TryGetCircuit`, `TryGetSource`, `TryGetBus` and must retain duplicate `(EntityKind, EntityUid)` pairs so the validator can report them rather than allowing dictionary construction to throw.

- [ ] **Step 6: Add the minimal `SingleLineInputValidator` orchestration so the duplicate test passes**

Public API:

```csharp
public sealed class SingleLineInputValidator
{
    public InputValidationResult Validate(SingleLineInput input);
}
```

At this step it may call private/index validation only; topology/completeness are added in Task 7.

- [ ] **Step 7: Run tests and commit**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release
git add src/UI_Unilineal.Engine/Validation tests/UI_Unilineal.Engine.Tests/Fixtures tests/UI_Unilineal.Engine.Tests/Validation
git commit -m "feat(engine): add typed input validation foundation"
```

---

### Task 7: Referential, topology and completeness validation

**Files:**
- Create: `src/UI_Unilineal.Engine/Validation/SupplyTopologyValidator.cs`
- Create: `src/UI_Unilineal.Engine/Validation/CompletenessValidator.cs`
- Modify: `src/UI_Unilineal.Engine/Validation/SingleLineInputValidator.cs`
- Modify/Test: `tests/UI_Unilineal.Engine.Tests/Validation/SingleLineInputValidatorTests.cs`

**Interfaces:**
- Consumes: `SingleLineInput`, `SemanticEntityIndex`.
- Produces: complete G1 validation behavior used as the mandatory gate before G2 projection.

- [ ] **Step 1: Add failing tests for every structural rule**

Add individual tests proving these exact outcomes:

```text
Circuit.BoardUid missing                  -> MISSING_REFERENCE / Error
Supply destination board missing          -> MISSING_REFERENCE / Error
Supply origin kind Circuit/Protection/... -> INVALID_SUPPLY_ORIGIN / Error
Board origin + no ThroughCircuitUid        -> BOARD_SUPPLY_REQUIRES_CIRCUIT / Error
Source origin + ThroughCircuitUid          -> SOURCE_SUPPLY_CANNOT_USE_BOARD_CIRCUIT / Error
Board origin circuit owned by other board -> SUPPLY_CIRCUIT_OWNER_MISMATCH / Error
Board origin equals destination            -> SELF_SUPPLY / Error
Normally-active board graph cycle          -> SUPPLY_CYCLE / Error
More than one BusRole.Main on same board   -> MULTIPLE_MAIN_BUSES / Error
Protection Owner missing                   -> MISSING_REFERENCE / Error
Protection Protects missing                -> MISSING_REFERENCE / Error
Grounding Owner missing                    -> MISSING_REFERENCE / Error
Result Entity missing                      -> MISSING_REFERENCE / Error
```

For source-origin supplies, valid origin kinds are exactly `Source` and `Board` in G1. A board-origin supply uses `ThroughCircuitUid`; the referenced circuit must belong to that origin board.

- [ ] **Step 2: Run the structural tests and verify RED**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~SingleLineInputValidatorTests
```

- [ ] **Step 3: Implement referential validation in `SingleLineInputValidator`**

Build `SemanticEntityIndex` once, append duplicate issues, then validate every circuit, bus, supply, protection, grounding and result reference. Do not throw for user data errors; return `ValidationIssue` instances.

- [ ] **Step 4: Implement active-cycle detection in `SupplyTopologyValidator`**

Only edges with `Origin.Kind == EntityKind.Board`, `State == OperationalState.Active` and `IsNormallyActive == true` participate in the active supply-cycle DFS. Alternative/emergency supplies that are not normally active do not make an otherwise valid transfer topology cyclic for this G1 rule. Self-supply is always an error regardless of normally-active state.

Use a three-state DFS (`Unvisited`, `Visiting`, `Visited`) keyed by board UID. Return one deterministic `SUPPLY_CYCLE` issue per detected cyclic strongly encountered path; sort starting boards by `Number`, `Code`, then UID before traversal so diagnostics are reproducible.

- [ ] **Step 5: Add failing warning tests**

```text
board has no incoming supply                    -> BOARD_WITHOUT_SUPPLY / Warning
active circuit Conductor == null                -> CIRCUIT_MISSING_CONDUCTOR / Warning
active circuit has no Breaker/Fuse/Combined     -> CIRCUIT_MISSING_PROTECTION / Warning
board NominalVoltageV == null                   -> BOARD_MISSING_VOLTAGE / Warning
source voltage or phase count missing           -> SOURCE_INCOMPLETE / Warning
grounding input has neither conductor section nor resistance -> GROUNDING_INCOMPLETE / Warning
```

Warnings must leave `InputValidationResult.HasErrors == false` when no structural errors exist.

- [ ] **Step 6: Implement `CompletenessValidator` and wire it into the orchestrator**

`SingleLineInputValidator.Validate` order is fixed:

```text
1. entity indexing / duplicate identity
2. referential integrity
3. supply topology
4. completeness warnings
5. stable sort of issues by Severity, Code, Entity.Kind, Entity.Uid, Field
```

Errors sort before warnings.

- [ ] **Step 7: Run full solution tests**

```bash
dotnet test UI_Unilineal.sln -c Release
```

Expected: all previous tests plus validation tests pass.

- [ ] **Step 8: Commit G1 validation behavior**

```bash
git add src/UI_Unilineal.Engine/Validation tests/UI_Unilineal.Engine.Tests/Validation
git commit -m "feat(engine): validate semantic topology and completeness"
```

---

### Task 8: Deterministic generated topology tests for G1

**Files:**
- Create: `tests/UI_Unilineal.Engine.Tests/Validation/GeneratedTopologyTests.cs`
- Modify: `tests/UI_Unilineal.Engine.Tests/Fixtures/SemanticFixtureFactory.cs`

**Interfaces:**
- Test-only generative evidence; no new production API.

- [ ] **Step 1: Add a deterministic chain generator**

Add to `SemanticFixtureFactory`:

```csharp
public static SingleLineInput BoardChain(int boardCount)
```

Requirements: `boardCount` from 1 through 50; one source `S1`; boards `B001`... with matching feeder circuits `F001`...; `S1 -> B001`, then `B001/F001 -> B002`, continuing without cycles. Every board/circuit is complete and every circuit has a breaker so generated valid chains produce no errors.

- [ ] **Step 2: Write generated validity test**

```csharp
[Fact]
public void GeneratedAcyclicChains_AreStructurallyValid()
{
    var validator = new SingleLineInputValidator();

    for (int count = 1; count <= 50; count++)
    {
        InputValidationResult result = validator.Validate(SemanticFixtureFactory.BoardChain(count));
        Assert.False(result.HasErrors, $"Unexpected error for board count {count}: {string.Join("; ", result.Issues)}");
    }
}
```

- [ ] **Step 3: Write generated cycle-injection test**

For chain lengths 2 through 50, create a copy with one extra normally-active supply from the last board, using a circuit owned by the last board, back to `B001`. Assert `SUPPLY_CYCLE` appears. Keep generated IDs deterministic so a failing count is reproducible.

- [ ] **Step 4: Run generated tests**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~GeneratedTopologyTests
```

Expected: PASS for all 99 generated scenarios.

- [ ] **Step 5: Commit G1 generated evidence**

```bash
git add tests/UI_Unilineal.Engine.Tests/Fixtures/SemanticFixtureFactory.cs tests/UI_Unilineal.Engine.Tests/Validation/GeneratedTopologyTests.cs
git commit -m "test(engine): exercise generated supply topologies"
```

---

### Task 9: G2 projection contracts

**Files:**
- Create: `src/UI_Unilineal.Engine/Projection/ProjectionStatus.cs`
- Create: `src/UI_Unilineal.Engine/Projection/ProjectionIssue.cs`
- Create: `src/UI_Unilineal.Engine/Projection/SummaryProjection.cs`
- Create: `src/UI_Unilineal.Engine/Projection/BoardDetailProjection.cs`
- Create: `src/UI_Unilineal.Engine/Projection/SingleLineProjection.cs`
- Create: `src/UI_Unilineal.Engine/Projection/ProjectionBuildResult.cs`
- Test: `tests/UI_Unilineal.Engine.Tests/Projection/SingleLineProjectionBuilderTests.cs` with compile-time contract assertions first

**Interfaces:**
- Produces: renderer-independent, geometry-free projection DTOs.

- [ ] **Step 1: Write a failing contract test that constructs expected projection records**

The test must compile against these exact public concepts:

```text
ProjectionStatus: Ok, Warning, Error, Pending, Stale, Unknown
ProjectionIssue
SummaryProjection / SummaryNode / SummaryConnection
BoardDetailProjection / IncomingSupplyProjection / BusProjection
BranchProjection / BranchDestination
SingleLineProjection
ProjectionBuildResult
```

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~SingleLineProjectionBuilderTests
```

- [ ] **Step 3: Implement projection records with these signatures**

Use these core declarations; collections passed by builders are already fresh arrays and are exposed as `IReadOnlyList<T>`:

```csharp
public enum ProjectionStatus { Ok, Warning, Error, Pending, Stale, Unknown }
public enum BranchKind { FinalCircuit, DownstreamBoard, Spare, Unknown }
public enum DestinationKind { Load, DownstreamBoard, External, Unknown }

public sealed record ProjectionIssue(
    string Code,
    ValidationSeverity Severity,
    string Message,
    EntityReference? Entity,
    string? Field);

public sealed record SummaryNode(
    EntityReference Entity,
    string Code,
    string Name,
    BoardRole? BoardRole,
    ProjectionStatus Status,
    int AlternateSupplyCount,
    int IssueCount);

public sealed record SummaryConnection(
    EntityUid SupplyConnectionUid,
    EntityReference Origin,
    EntityUid? ThroughCircuitUid,
    EntityReference Destination,
    SupplyRole Role,
    bool IsNormallyActive,
    ProjectionStatus Status);

public sealed record SummaryProjection(
    IReadOnlyList<SummaryNode> Nodes,
    IReadOnlyList<SummaryConnection> Connections,
    IReadOnlyList<EntityReference> Roots,
    IReadOnlyList<ProjectionIssue> Issues);

public sealed record IncomingSupplyProjection(
    SupplyConnection Supply,
    SourceInput? Source,
    BoardInput? OriginBoard,
    CircuitInput? ThroughCircuit,
    ProjectionStatus Status);

public sealed record BusProjection(
    BusInput Bus,
    IReadOnlyList<ProtectionInput> MainProtections,
    ProjectionStatus Status,
    int IssueCount);

public sealed record BranchDestination(
    DestinationKind Kind,
    EntityReference? Entity,
    string Label,
    EntityReference? NavigationTarget);

public sealed record BranchProjection(
    CircuitInput Circuit,
    IReadOnlyList<ProtectionInput> ProtectionChain,
    ElectricalResultInput? Result,
    BranchDestination Destination,
    BranchKind Kind,
    ProjectionStatus Status,
    IReadOnlyList<ProjectionIssue> Issues);

public sealed record BoardDetailProjection(
    BoardInput Board,
    IReadOnlyList<IncomingSupplyProjection> IncomingSupplies,
    BusProjection MainBus,
    IReadOnlyList<BranchProjection> Branches,
    IReadOnlyList<GroundingInput> Grounding,
    IReadOnlyList<ProjectionIssue> Issues,
    IReadOnlyList<EntityReference> NavigationTargets);

public sealed record SingleLineProjection(
    EntityUid ProjectUid,
    SummaryProjection Summary,
    IReadOnlyList<BoardDetailProjection> BoardDetails,
    IReadOnlyList<ProjectionIssue> Issues,
    string InputFingerprint);

public sealed record ProjectionBuildResult(
    SingleLineProjection? Projection,
    InputValidationResult Validation)
{
    public bool Success => Projection is not null && !Validation.HasErrors;
}
```

- [ ] **Step 4: Run tests and commit contracts**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release
git add src/UI_Unilineal.Engine/Projection tests/UI_Unilineal.Engine.Tests/Projection/SingleLineProjectionBuilderTests.cs
git commit -m "feat(engine): define semantic projection contracts"
```

---

### Task 10: Deterministic input fingerprint and projection status resolver

**Files:**
- Create: `src/UI_Unilineal.Engine/Projection/SingleLineInputFingerprint.cs`
- Create: `src/UI_Unilineal.Engine/Projection/ProjectionStatusResolver.cs`
- Create/Test: `tests/UI_Unilineal.Engine.Tests/Projection/SingleLineInputFingerprintTests.cs`
- Create/Test: `tests/UI_Unilineal.Engine.Tests/Projection/ProjectionStatusResolverTests.cs`

**Interfaces:**
- Produces: `SingleLineInputFingerprint.Compute(SingleLineInput) -> string` and `ProjectionStatusResolver.Resolve(DataState, ResultState?, IEnumerable<ProjectionIssue>)`.

- [ ] **Step 1: Write fingerprint permutation test**

Construct the same semantic input twice with Sources, Boards, Buses, Circuits, SupplyConnections, Protections, Grounding and Results reversed/shuffled. Assert identical uppercase SHA-256 hex fingerprints.

Also change one circuit name and assert the fingerprint changes.

- [ ] **Step 2: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~SingleLineInputFingerprintTests
```

- [ ] **Step 3: Implement canonical fingerprint**

`SingleLineInputFingerprint.Compute` creates an anonymous normalized object with each collection sorted by semantic UID; `Results` sort by `Entity.Kind` then `Entity.Uid.Value`. Serialize with `JsonSerializer.Serialize` using default invariant JSON number formatting, hash UTF-8 bytes with `SHA256.HashData`, return `Convert.ToHexString(hash)`. Do not include current time, process data or enumeration order from dictionaries.

- [ ] **Step 4: Write status precedence tests**

Required precedence:

```text
any Error issue                  -> Error
any Warning issue                -> Warning
DataState.Invalid                -> Error
DataState.Incomplete             -> Warning
ResultState.Pending              -> Pending
ResultState.Stale                -> Stale
ResultState.Missing/Unknown      -> Unknown
complete + Current/no result     -> Ok
```

An Error/Warning issue outranks result state.

- [ ] **Step 5: Implement `ProjectionStatusResolver` and run tests**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~SingleLineInputFingerprintTests|FullyQualifiedName~ProjectionStatusResolverTests"
```

Expected: PASS.

- [ ] **Step 6: Commit determinism primitives**

```bash
git add src/UI_Unilineal.Engine/Projection tests/UI_Unilineal.Engine.Tests/Projection
git commit -m "feat(engine): add deterministic projection fingerprinting"
```

---

### Task 11: Summary projection builder

**Files:**
- Create: `src/UI_Unilineal.Engine/Projection/SummaryProjectionBuilder.cs`
- Create/Test: `tests/UI_Unilineal.Engine.Tests/Projection/SummaryProjectionBuilderTests.cs`

**Interfaces:**
- Internal API: `SummaryProjection Build(SingleLineInput input, SemanticEntityIndex index, IReadOnlyList<ProjectionIssue> issues)`.
- Produces: source/board summary nodes, supply connections and roots; no final circuits are summary nodes.

- [ ] **Step 1: Write failing minimal-summary test**

For `SemanticFixtureFactory.Minimal()` assert:

```text
Nodes              = 2 (S1 Source, B1 Board)
Connections        = 1 (SC1)
Roots              = [Source S1]
B1 alternate count = 0
No Circuit C1 node appears in summary
```

- [ ] **Step 2: Write failing multi-supply test**

Build one board with two source supplies: one `Normal` normally active, one `Emergency` not normally active. Assert the board node `AlternateSupplyCount == 1`, both `SummaryConnection` objects exist, and root list contains both sources ordered by source code/UID.

- [ ] **Step 3: Run and verify RED**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~SummaryProjectionBuilderTests
```

- [ ] **Step 4: Implement deterministic summary ordering**

Ordering contract:

```text
Sources: Code (OrdinalIgnoreCase), Uid.Value (Ordinal)
Boards: Number, Code (OrdinalIgnoreCase), Uid.Value (Ordinal)
Connections: destination Board.Number, Supply.Priority, Supply.Role, Supply.Uid.Value
Roots: sources in source ordering, then orphan boards in board ordering
```

A root board is a board without any active incoming supply. It remains visible and carries the validation warning `BOARD_WITHOUT_SUPPLY`.

Map validation issues to `ProjectionIssue` without changing code/severity/message/entity/field.

- [ ] **Step 5: Run summary tests and commit**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~SummaryProjectionBuilderTests
git add src/UI_Unilineal.Engine/Projection/SummaryProjectionBuilder.cs tests/UI_Unilineal.Engine.Tests/Projection/SummaryProjectionBuilderTests.cs
git commit -m "feat(engine): build deterministic project summary projection"
```

---

### Task 12: Board-detail projection builder

**Files:**
- Create: `src/UI_Unilineal.Engine/Projection/BoardDetailProjectionBuilder.cs`
- Create/Test: `tests/UI_Unilineal.Engine.Tests/Projection/BoardDetailProjectionBuilderTests.cs`
- Modify: `tests/UI_Unilineal.Engine.Tests/Fixtures/SemanticFixtureFactory.cs`

**Interfaces:**
- Internal API: `BoardDetailProjection Build(BoardInput board, SingleLineInput input, SemanticEntityIndex index, IReadOnlyList<ProjectionIssue> issues)`.
- Produces: incoming supplies, main bus, branches, protection chains, downstream navigation and board grounding.

- [ ] **Step 1: Add `SemanticFixtureFactory.NestedBoards()`**

Fixture identity contract:

```text
Project P2
Source S1 / EMPALME
Board B1 #1 / TGBT
Board B2 #2 / TDA
Supply SC1: Source S1 -> B1
Circuit C4: owned by B1, #4, code C04, role Feeder
Supply SC2: Board B1 through C4 -> B2
Circuit C2: owned by B2, #1, code C01, role Final
Breaker PR4 owns/protects C4
Breaker PR2 owns/protects C2
```

All electrical data needed to avoid completeness warnings is populated.

- [ ] **Step 2: Write failing nested-board detail tests**

For B1 assert branch C4 is `BranchKind.DownstreamBoard`, destination entity is `Board B2`, navigation target is `Board B2`, and `NavigationTargets` contains B2 exactly once.

For B2 assert C2 is `BranchKind.FinalCircuit`, destination label equals circuit name and navigation target is null.

- [ ] **Step 3: Write failing synthetic-main-bus test**

With no explicit bus in Minimal fixture, assert detail contains:

```text
MainBus.Bus.Uid   = BUS:B1:MAIN
MainBus.Bus.Code  = MAIN
MainBus.Bus.Role  = Main
MainBus.Bus.BoardUid = B1
```

Synthetic bus creation does not mutate `SingleLineInput.Buses`.

- [ ] **Step 4: Write failing protection/result tests**

Assert C1 protection chain contains PR1 and current result is attached. Chain ordering is by a fixed role order `Main, Feeder, Branch, Adopted, Recommended, Backup, Other`, then UID. The builder does not invent a missing protection.

- [ ] **Step 5: Implement board detail builder**

Incoming supply order: `Priority`, `Role`, `Uid`.

Branch order: `Circuit.Number`, `Circuit.Code` OrdinalIgnoreCase, `Circuit.Uid` Ordinal.

Grounding order: UID.

Explicit main bus: use the unique `BusRole.Main` for the board. No explicit main bus: synthesize deterministic `BUS:<boardUid>:MAIN`. Multiple explicit main buses never reach successful projection because validator reports an Error.

Downstream destination resolution: find supply connections whose origin is this board and whose `ThroughCircuitUid` equals the branch circuit UID. Exactly one destination -> `DownstreamBoard`; zero -> `FinalCircuit` unless `CircuitRole.Reserved`, which maps to `Spare`. If more than one destination is encountered despite validation evolution, emit `BranchKind.Unknown` rather than picking an arbitrary destination.

- [ ] **Step 6: Run detail tests and commit**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~BoardDetailProjectionBuilderTests
git add src/UI_Unilineal.Engine/Projection/BoardDetailProjectionBuilder.cs tests/UI_Unilineal.Engine.Tests/Fixtures/SemanticFixtureFactory.cs tests/UI_Unilineal.Engine.Tests/Projection/BoardDetailProjectionBuilderTests.cs
git commit -m "feat(engine): build board detail projections"
```

---

### Task 13: Projection orchestration, invalid-input blocking and permutation determinism

**Files:**
- Create: `src/UI_Unilineal.Engine/Projection/SingleLineProjectionBuilder.cs`
- Modify/Test: `tests/UI_Unilineal.Engine.Tests/Projection/SingleLineProjectionBuilderTests.cs`

**Interfaces:**
- Public API: `ProjectionBuildResult SingleLineProjectionBuilder.Build(SingleLineInput input)`.
- Consumes: validator, fingerprint, summary/detail builders.
- Produces: one immutable projection snapshot or a validation-only failure result.

- [ ] **Step 1: Write failing success-path test**

```csharp
[Fact]
public void Build_ValidInput_ReturnsSummaryAndEveryBoardDetail()
{
    var input = SemanticFixtureFactory.NestedBoards();

    ProjectionBuildResult result = new SingleLineProjectionBuilder().Build(input);

    Assert.True(result.Success);
    Assert.NotNull(result.Projection);
    Assert.Equal(2, result.Projection.BoardDetails.Count);
    Assert.Equal(input.Project.Uid, result.Projection.ProjectUid);
}
```

- [ ] **Step 2: Write failing structural-error blocking test**

Create an invalid cycle fixture. Assert `Success == false`, `Projection == null`, and `Validation.Issues` contains `SUPPLY_CYCLE`. The builder must not attempt partial G2 output when an Error exists; partial error-scene behavior can be designed in a later package if required.

- [ ] **Step 3: Write failing warning-tolerance test**

Remove conductor from C1 but keep structure valid. Assert projection is produced, validation contains `CIRCUIT_MISSING_CONDUCTOR`, and the affected branch status is `Warning`.

- [ ] **Step 4: Write permutation determinism test**

Build a semantic clone of NestedBoards with every input collection reversed. Build both and assert:

```text
InputFingerprint equal
Summary nodes sequence equal
Summary connections sequence equal
Board detail sequence equal
Branch sequences equal
```

Do not sort in the test before comparing; the production builder must establish stable order.

- [ ] **Step 5: Implement orchestration**

Required algorithm:

```text
validate input
if HasErrors -> ProjectionBuildResult(null, validation)
map validation issues -> ProjectionIssue[]
create SemanticEntityIndex
compute input fingerprint
build SummaryProjection
build BoardDetailProjection for boards ordered Number/Code/Uid
return SingleLineProjection + same validation result
```

- [ ] **Step 6: Run all Engine tests**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release
```

- [ ] **Step 7: Commit projection orchestrator**

```bash
git add src/UI_Unilineal.Engine/Projection/SingleLineProjectionBuilder.cs tests/UI_Unilineal.Engine.Tests/Projection/SingleLineProjectionBuilderTests.cs
git commit -m "feat(engine): complete validated single-line projection pipeline"
```

---

### Task 14: Structural projection goldens

**Files:**
- Create: `tests/UI_Unilineal.Engine.Tests/Projection/ProjectionGoldenFormatter.cs`
- Create: `tests/UI_Unilineal.Engine.Tests/Projection/ProjectionGoldenTests.cs`
- Create: `tests/UI_Unilineal.Engine.Tests/Golden/Projection/minimal-project.txt`
- Create: `tests/UI_Unilineal.Engine.Tests/Golden/Projection/nested-boards.txt`
- Modify: `tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj` only if required to copy/read golden files; prefer locating them from `AppContext.BaseDirectory` with `<None Include="Golden/**" CopyToOutputDirectory="PreserveNewest" />`.

**Interfaces:**
- Test-only canonical textual representation of projection structure; this is reviewed evidence, not a production serialization format.

- [ ] **Step 1: Add golden files to test output**

If needed, add:

```xml
<ItemGroup>
  <None Include="Golden\**\*" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

- [ ] **Step 2: Implement a stable formatter**

`ProjectionGoldenFormatter.Format` writes one LF-terminated line per semantic item, in projection order, using this exact grammar:

```text
PROJECT|<projectUid>
ROOT|<kind>|<uid>
SUMMARY_NODE|<kind>|<uid>|<code>|<status>|<issueCount>
SUMMARY_CONNECTION|<supplyUid>|<originKind>|<originUid>|<throughCircuitOrDash>|<destinationUid>|<role>|<normallyActive>|<status>
DETAIL|<boardUid>|<boardCode>|<status>
BUS|<busUid>|<busCode>|<status>|<mainProtectionCount>
BRANCH|<circuitUid>|<circuitCode>|<branchKind>|<destinationUidOrDash>|<status>|<protectionCount>
```

Do not include the SHA fingerprint in goldens; determinism of the fingerprint already has direct tests.

- [ ] **Step 3: Write golden comparison tests before creating accepted files**

Test loads each fixture, requires successful projection, formats it, normalizes `\r\n` to `\n`, reads the corresponding checked-in golden and compares exact strings.

- [ ] **Step 4: Create and manually inspect `minimal-project.txt`**

Accepted content:

```text
PROJECT|P1
ROOT|Source|S1
SUMMARY_NODE|Source|S1|EMPALME|Ok|0
SUMMARY_NODE|Board|B1|TGBT|Ok|0
SUMMARY_CONNECTION|SC1|Source|S1|-|B1|Normal|True|Ok
DETAIL|B1|TGBT|Ok
BUS|BUS:B1:MAIN|MAIN|Ok|0
BRANCH|C1|C01|FinalCircuit|-|Ok|1
```

- [ ] **Step 5: Create and manually inspect `nested-boards.txt`**

Accepted content, assuming the fixture uses B1/TGBT #1 and B2/TDA #2:

```text
PROJECT|P2
ROOT|Source|S1
SUMMARY_NODE|Source|S1|EMPALME|Ok|0
SUMMARY_NODE|Board|B1|TGBT|Ok|0
SUMMARY_NODE|Board|B2|TDA|Ok|0
SUMMARY_CONNECTION|SC1|Source|S1|-|B1|Normal|True|Ok
SUMMARY_CONNECTION|SC2|Board|B1|C4|B2|Normal|True|Ok
DETAIL|B1|TGBT|Ok
BUS|BUS:B1:MAIN|MAIN|Ok|0
BRANCH|C4|C04|DownstreamBoard|B2|Ok|1
DETAIL|B2|TDA|Ok
BUS|BUS:B2:MAIN|MAIN|Ok|0
BRANCH|C2|C01|FinalCircuit|-|Ok|1
```

If the actual deterministic formatter differs from these lines, inspect the difference as a design/test bug. Do not auto-accept the output; either correct production ordering/status behavior or deliberately update this plan/spec before changing the accepted contract.

- [ ] **Step 6: Run golden tests twice**

```bash
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~ProjectionGoldenTests
dotnet test tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj -c Release --filter FullyQualifiedName~ProjectionGoldenTests
```

Expected: both runs pass identically.

- [ ] **Step 7: Commit G2 golden evidence**

```bash
git add tests/UI_Unilineal.Engine.Tests/Projection tests/UI_Unilineal.Engine.Tests/Golden tests/UI_Unilineal.Engine.Tests/UI_Unilineal.Engine.Tests.csproj
git commit -m "test(engine): freeze G2 projection goldens"
```

---

### Task 15: G2 checkpoint verification and repository documentation

**Files:**
- Modify: `README.md`
- Modify: `docs/architecture/README.md`

**Interfaces:**
- Produces: documented checkpoint saying exactly what exists after G2 and what is still absent.

- [ ] **Step 1: Update README status**

Replace the scaffold-only status with:

```markdown
## Estado

V1 G0–G2 implementado:

- baseline CI en Windows/Linux;
- `SingleLineInput` semántico e inmutable;
- topología explícita mediante `SupplyConnection`;
- validación referencial, de ciclos y de datos incompletos;
- `SingleLineProjection` resumen/detalle determinista;
- pruebas generativas y goldens estructurales.

Aún no forman parte de este checkpoint: perfil gráfico RIC18, `DiagramScene`, layout, renderer Avalonia V1, interacción productiva, SVG/PDF e integración con `ProyectoElectrico`.
```

- [ ] **Step 2: Update architecture README with the implemented pipeline**

Document the actual G2 flow:

```text
SingleLineInput
      ↓
SingleLineInputValidator
      ↓
SingleLineProjectionBuilder
      ├─ SummaryProjection
      └─ BoardDetailProjection[]
```

State explicitly that the next package G3–G5 starts at profile/composition/scene/layout and must not move validation or host concerns into Domain.

- [ ] **Step 3: Run the complete G0–G2 verification gate**

```bash
dotnet restore UI_Unilineal.sln
dotnet format UI_Unilineal.sln --verify-no-changes --no-restore
dotnet build UI_Unilineal.sln -c Release --no-restore
dotnet test UI_Unilineal.sln -c Release --no-build --logger "console;verbosity=normal"
git status --short
```

Expected:

```text
restore exit 0
format exit 0
build exit 0 with 0 warnings/errors
test exit 0; all old + G0–G2 tests pass
git status lists only README/docs changes intended for this task before commit
```

- [ ] **Step 4: Commit the G2 checkpoint documentation**

```bash
git add README.md docs/architecture/README.md
git commit -m "docs: mark V1 G0-G2 semantic pipeline checkpoint"
```

- [ ] **Step 5: Re-run verification on the committed tree**

```bash
dotnet build UI_Unilineal.sln -c Release
dotnet test UI_Unilineal.sln -c Release
git status --short
```

Expected: build/test green and `git status --short` empty.

- [ ] **Step 6: Record the checkpoint commit SHA in the implementation session report**

Use:

```bash
git rev-parse HEAD
```

The executor's completion response must report that SHA together with exact build/test results; do not create a tag yet. Tags/releases begin after later package gates define their policy.

---

## Self-Review Notes for This Plan

**Spec coverage:** This plan intentionally covers only master-spec G0, G1 and G2. It covers baseline CI, dependency boundaries, stable identity, all approved semantic input entity families, explicit supply topology, immutable aggregate behavior, structural/completeness validation, cycle detection, deterministic fingerprinting, summary/detail projection, downstream-board navigation semantics, warning tolerance, invalid-input blocking and projection goldens. G3–G12 remain outside this implementation package exactly as required by the approved decomposition.

**Scope decisions made explicit:** In G2, any validation `Error` blocks projection entirely; warnings permit projection. Supply origin is either `Source` (no through circuit in G1) or `Board` (through circuit required and owned by origin board). Active cycle detection considers normally-active active board-to-board supplies; normally-inactive alternate/emergency edges do not create an active-topology cycle. A missing main bus is synthesized deterministically; more than one explicit main bus is an input error. These choices remove ambiguity for the executor without adding host-specific semantics.

**Type consistency:** Domain uses `EntityUid`/`EntityReference`; no numeric host IDs occur. Validation types live in Engine and are referenced by projection diagnostics. `SingleLineProjectionBuilder.Build(SingleLineInput)` is the only public orchestration entry point introduced in G2. No G3 geometry/profile types are referenced.

**No package expansion:** G0–G2 use the existing xUnit stack and BCL only. The generated topology suite is deterministic xUnit generation; richer property-based shrinking tooling is intentionally part of G9 hardening, so this package does not introduce an unverified external package version.
