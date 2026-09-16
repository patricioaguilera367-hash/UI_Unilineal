# UI_Unilineal Architecture

UI_Unilineal es un subsistema independiente para generación,
visualización e interacción con diagramas unilineales.

## Frontera

El motor NO depende de:

- ProyectoElectrico main
- ProjectSnapshot
- .peproj
- CSV
- Infrastructure.Csv
- Desktop
- entidades concretas Board/Circuit del proyecto principal

La futura integración se realizará mediante un adaptador:

ProyectoElectrico
        |
        v
SingleLineIntegrationAdapter
        |
        v
SingleLineInput
        |
        v
SingleLineProjection
        |
        v
SingleLineLayoutEngine
        |
        v
DiagramScene

## Dependencias

Domain
  ^
  |
Engine

Domain
  ^
  |
Rendering.Avalonia

Domain + Engine + Rendering.Avalonia
  ^
  |
Playground

## Regla fundamental

DiagramScene será independiente del renderer.

Avalonia, PDF, SVG y DXF deberán consumir la misma escena gráfica
en lugar de implementar generadores de unilineal independientes.
