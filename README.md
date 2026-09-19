# UI_Unilineal

Motor independiente para generación de diagramas unilineales eléctricos.

## Objetivo

Desarrollar un subsistema desacoplado capaz de producir:

1. Unilineal resumen del proyecto.
2. Unilineal detallado por tablero.
3. Visualización interactiva.
4. Navegación y revisión.
5. Edición futura mediante adaptadores.
6. Exportación vectorial.
7. Integración futura con ProyectoElectrico.

## Arquitectura objetivo

SingleLineInput
      |
      v
SingleLineProjection
      |
      v
RIC18 Drawing Profile
      |
      v
SingleLineLayoutEngine
      |
      v
DiagramScene
   /   |    |   \
  /    |    |    \
Avalonia PDF SVG DXF

## Regla de desacoplamiento

UI_Unilineal no conoce el modelo interno de ProyectoElectrico.

La integración final se realizará mediante un adaptador entre el modelo
canónico del proyecto principal y `SingleLineInput`.

## Proyectos

### UI_Unilineal.Domain

Modelo semántico independiente:

- Semantics
- Symbols
- Blocks
- Connections
- Scene
- Profiles

### UI_Unilineal.Engine

Transformación y composición:

- Projection
- Layout
- Composition
- Validation

### UI_Unilineal.Rendering.Avalonia

Adaptador gráfico:

- Rendering
- HitTesting
- Interaction

### UI_Unilineal.Playground

Aplicación de desarrollo independiente para probar el motor sin
ProyectoElectrico main.

### Tests

- UI_Unilineal.Domain.Tests
- UI_Unilineal.Engine.Tests
- UI_Unilineal.Rendering.Avalonia.Tests

## Estado

V1 G0–G8 implementado y cubierto por CI Windows/Linux:

- `SingleLineInput` semántico e inmutable y topología explícita mediante
  `SupplyConnection`;
- validación referencial/topológica y `SingleLineProjection` determinista;
- `RIC18DrawingProfile` versionado, validado y con procedencia explícita;
- `DrawingComposition` renderer-neutral y sin coordenadas;
- `DiagramScene` vectorial en milímetros con identidad, kind, issues,
  metadata y fingerprint SHA-256;
- anchors/connections por `SceneId + AnchorId`;
- medición mediante `ITextMetrics` inyectable; los tests usan métricas
  deterministas sin imponerlas al runtime;
- layout resumen por profundidad eléctrica y detalle por ramas con wrapping;
- routing ortogonal con clearance y validación estricta independiente de
  rutas que atraviesan bloques estructurales;
- resolución determinista de colisiones estructurales;
- `DiagramLayoutState` presentation-only con `Auto / Pinned / Locked`;
- estabilización incremental que preserva posiciones existentes siempre que
  las invariantes lo permitan;
- `SingleLineLayoutEngine` orquesta el pipeline completo y devuelve
  failures tipados;
- goldens revisados de summary/detail mínimo y nested;
- stress end-to-end de 1/4/12/24/48/100 circuitos y equivalencia ante
  reordenamiento de colecciones;
- guardas de arquitectura de assembly + source contra Avalonia y
  `ProyectoElectrico`.

El pipeline productivo de G5 es:

```text
Measure -> Place -> Resolve -> Assemble -> Route -> Validate -> Scene
```

`Resolve` ocurre antes de `Route` deliberadamente: mover bloques después de
calcular una ruta invalidaría sus endpoints/obstáculos. Esta corrección quedó
incorporada a la especificación durante la self-review de G5.

Toda geometría inicial no sustentada por una cita normativa exacta permanece
clasificada como `APP_CONVENTION`.

### G6 — renderer Avalonia

G6 implementa la visualización interactiva sin trasladar estado de edición
ni decisiones eléctricas al renderer:

```text
DiagramScene -> SceneSpatialIndex -> Viewport -> AvaloniaSceneRenderer -> SingleLineView
```

- `SceneSpatialIndex` realiza culling en coordenadas de escena (mm);
- `ViewportState`, `ViewportTransform` y `ViewportController` mantienen
  zoom, pan, fit y conversiones DIP/mm fuera de `DiagramScene`;
- `HitTestIndex` usa tolerancias de interacción en DIPs y orden de hits
  determinista, independiente del grosor gráfico;
- `AvaloniaRenderResources` resuelve recursos del perfil con caches
  acotadas;
- `AvaloniaSceneRenderer` dibuja la escena en immediate mode y conserva su
  fingerprint;
- `SingleLineView` es un único `Control` inmediato: compone viewport,
  índice, hit-test, renderer y overlays pasivos sin crear controles por
  elemento;
- el Playground posee la navegación Summary/BoardDetail/Back y los
  controles Zoom/Fit; esa navegación no pertenece al renderer;
- el hardening G6 incluye una escena G5 de 48 circuitos que verifica culling,
  render headless, hit-testing estable e inmutabilidad de la escena.

El Playground queda disponible para la aceptación visual/runtime local con
`dotnet run --project src\UI_Unilineal.Playground\UI_Unilineal.Playground.csproj -c Release`.
La CI headless no sustituye esa inspección manual.

### G7 — interacción y comandos

G7 incorpora interacción explícita sin trasladar autoridad eléctrica al
renderer:

```text
SingleLineView gesture
      |
      v
neutral intent
      |
      v
Playground shell
   /          \
  v            v
Layout       ElectricalCommandProposal
history            |
  |                v
  v        IElectricalCommandHandler
rebuild             |
scene          Applied only
                   |
                   v
          rebuild input/projection/scene
```

- Navigate / Layout / Electrical son modos explícitos y mutuamente
  excluyentes, gobernados por una única máquina de estados determinista;
- hover, selección, marquee, drag ghosts y previews siguen siendo overlays y
  no se almacenan en `DiagramScene`;
- los cambios de layout son presentation-only, se ejecutan mediante
  `LayoutCommandHistory` local reversible y reconstruyen una escena
  inmutable con `SingleLineLayoutEngine`;
- los gestos eléctricos sólo nacen desde anchors semánticos en modo
  Electrical y producen una `ElectricalCommandProposal` inmutable;
- el host se representa mediante `IElectricalCommandHandler`, requests con
  `ExpectedRevision` y resultados tipados
  `Applied / Rejected / NeedsConfirmation / Conflict / Failed`;
- no existe mutación eléctrica optimista: sólo `Applied` reemplaza el
  snapshot, reproyecta y reconstruye la escena;
- el undo eléctrico queda representado como un comando inverso enviado al
  host contra la revisión vigente; no fuerza rollback stale;
- `HostCapabilities` permite degradar a read-only sin perder navegación,
  selección ni visualización;
- el Playground posee la orquestación, histories y demo host; Rendering
  traduce input y dibuja overlays, pero no ejecuta histories ni comandos del
  host.

El hardening de G7 cubre matrices exhaustivas pequeñas de transición/cancel,
secuencias model-based de layout execute/undo/redo/cancel, resultados
Rejected/Conflict sin mutación de escena y guards explícitos de dependencia.
La self-review contra la especificación §§14.4, 15, 16 y 17 mantiene las
fronteras previstas: overlays fuera de la escena, comandos eléctricos fuera
del renderer, navegación en el shell y capacidades explícitas.

El Playground queda disponible para aceptación visual/runtime local con
`dotnet run --project src\\UI_Unilineal.Playground\\UI_Unilineal.Playground.csproj -c Release`.
La CI headless no sustituye esa inspección manual.

### G8 — documentos y exportación

G8 añade composición física y exportación vectorial sin convertir al renderer
ni al Playground en fuentes alternativas de geometría:

```text
DiagramScene
      |
      v
DocumentComposer
      |
      v
DrawingDocument / DrawingSheet[]
      |                 |
      v                 v
SvgExporter         PdfExporter
```

- `DrawingDocument` y `DrawingSheet` separan papel, escala, márgenes,
  viewport, title block y continuaciones del estado de pantalla;
- A0–A4 y Custom usan dimensiones físicas explícitas en milímetros;
- `DocumentPreflight` valida estilos, fonts, geometría y compatibilidad antes
  de escribir bytes;
- `ExportManifest` registra fingerprints semánticos, perfil, layout,
  exporter, revisión y cantidad de hojas sin introducir timestamps;
- SVG es determinista, autocontenido, seguro frente a texto XML hostil y
  permanece vector/text;
- PDF es determinista, multipágina, conserva MediaBox físico y utiliza
  operadores vectoriales/texto sin image XObjects para primitivas soportadas;
- ambos exporters consumen exactamente el mismo `DrawingDocument`;
- los exporters de bajo nivel escriben a `Stream` y observan cancelación;
- el Playground sólo orquesta exportación capability-gated y realiza
  reemplazo atómico en el boundary de host;
- architecture guards impiden dependencias Avalonia o `ProyectoElectrico`
  dentro de los exporters.

La evidencia G8 incluye goldens estructurales cross-format, equivalencia ante
input semánticamente reordenado, casos Unicode/XML/PDF hostiles, geometría de
papel límite, cancelación, atomicidad y CI Windows/Linux.

La inspección visual/manual sigue siendo complementaria a la evidencia
estructural headless; la CI no pretende certificar por sí sola calidad
tipográfica o apreciación visual final.

El siguiente checkpoint es **G9: hardening**. La integración real de lectura
con `ProyectoElectrico` comienza en G10.
