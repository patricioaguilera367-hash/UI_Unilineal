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

## Checkpoint implementado: V1 G3

G3 añade una frontera gráfica explícita sin introducir layout:

```text
SingleLineProjection
      |
      v
RIC18DrawingProfile
      |
      v
CompositionBuilder
      |
      v
DrawingComposition
```

`RIC18DrawingProfile` contiene símbolos vectoriales, bloques, estilos,
constantes de layout y procedencia. `DrawingProfileValidator` impide IDs
duplicados, referencias rotas y clasificaciones RIC sin documento +
localizador explícito. La geometría semilla actual se declara
`APP_CONVENTION`; no se presenta como mandato de RIC N°18.

`DrawingComposition` describe qué bloques, labels, symbol overrides,
anchors semánticos y conexiones deben existir, pero no contiene
coordenadas, bounds de escena, tipos Avalonia ni estado eléctrico mutable.
El resumen deduplica entidades aunque existan múltiples supplies. El
detalle conserva cadenas de protección de longitud variable, downstream
boards navegables, cargas finales y grounding.

Los goldens estructurales de composición fijan los contratos de detalle
mínimo y tablero derivado. El siguiente checkpoint G4 introduce por
primera vez geometría de escena en milímetros mediante `DiagramScene`.
