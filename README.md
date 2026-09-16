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

Scaffold arquitectónico inicial.

Aún no contiene implementación del modelo semántico, layout ni
simbología RIC18.
