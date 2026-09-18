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

## Checkpoint implementado: V1 G0–G2

El flujo actualmente implementado y verificado es:

```text
SingleLineInput
      |
      v
SingleLineInputValidator
      |
      v
SingleLineProjectionBuilder
      |-- SummaryProjection
      `-- BoardDetailProjection[]
```

`SingleLineInput` es un snapshot semántico inmutable. La validación
referencial, topológica y de completitud pertenece a Engine; Domain se
mantiene como contrato de datos y no absorbe reglas del host ni
dependencias de UI.

`SingleLineProjectionBuilder` bloquea errores estructurales, conserva
warnings como diagnósticos proyectables, ordena la salida de forma
determinista y calcula un fingerprint SHA-256 independiente del orden de
las colecciones de entrada.

El siguiente paquete G3–G5 comienza en perfil gráfico/composición,
`DiagramScene` y layout. No debe mover validación, persistencia del host
ni concerns de `ProyectoElectrico` hacia Domain.
