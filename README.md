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

## Estado

V1 G0–G4 implementado:

- baseline CI en Windows/Linux;
- `SingleLineInput` semántico e inmutable;
- topología explícita mediante `SupplyConnection`;
- validación referencial, de ciclos y de datos incompletos;
- `SingleLineProjection` resumen/detalle determinista;
- `RIC18DrawingProfile` versionado, inmutable y validado;
- catálogo vectorial de símbolos/bloques con procedencia explícita;
- fingerprint SHA-256 determinista del perfil;
- `DrawingComposition` sin geometría para resumen y detalle;
- cadenas de protección variables, destinos navegables y grounding semántico;
- `DiagramScene` vectorial neutral en milímetros;
- `SceneId` jerárquicos y deterministas;
- anchors y conexiones de escena mediante `SceneId + AnchorId`;
- validación estructural y fingerprint SHA-256 determinista de escena;
- `SceneAssembly` para expandir composición ya posicionada a primitivas
  renderer-neutral;
- guardas de arquitectura a nivel assembly y fuente contra dependencias de
  Avalonia y `ProyectoElectrico`;
- goldens estructurales de proyección y composición.

Toda geometría inicial no sustentada por una cita normativa exacta permanece
clasificada como `APP_CONVENTION`.

G4 no implementa layout automático. `SceneAssembly` exige posiciones
explícitas y falla si falta alguna; por diseño no decide coordenadas.

El siguiente checkpoint es G5: medición determinista, estrategias de
posición, wrapping, routing ortogonal, resolución de colisiones, overrides
de layout y orquestación `Measure → Place → Route → Resolve → Validate → Scene`.
Renderer Avalonia V1, interacción productiva, SVG/PDF e integración con
`ProyectoElectrico` permanecen fuera de G4.
