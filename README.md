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

V1 G0–G6 implementado y cubierto por CI Windows/Linux:

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

El siguiente checkpoint es **G7: modos de interacción y comandos**. G7 posee
la máquina de estados y las propuestas/ejecución de comandos; G8 posee
composición documental y exportación. SVG/PDF, undo/redo eléctrico o de
layout e integración con `ProyectoElectrico` no forman parte de G6.
