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

## Checkpoint implementado: V1 G4

G4 introduce la escena gráfica neutral y mantiene separada la decisión de
layout:

```text
DrawingComposition
      |
      | posiciones ya calculadas
      v
SceneAssembly
      |
      v
DiagramScene
```

`DiagramScene` es renderer-neutral y expresa toda su geometría en
milímetros. Sus elementos usan `SceneId` jerárquicos y deterministas,
layers explícitos, z-order, visibility, bounds y referencias semánticas
opcionales. La escena admite line, polyline, rectangle, circle, path, text,
symbol y group sin tipos Avalonia.

Los anchors físicos pertenecen a los elementos de escena. Una
`SceneConnection` no duplica coordenadas de extremos: guarda únicamente
`SceneId + AnchorId` para source y target. De esta forma, routing y
rendering podrán operar sobre la misma topología gráfica sin crear una
segunda fuente de verdad.

`DiagramSceneValidator` verifica IDs duplicados, bounds inválidos,
anchors inexistentes, conexiones huérfanas, anchors fuera de sus elementos,
children de grupo huérfanos y elementos fuera del bounds global. Las
coordenadas no finitas ya son rechazadas por los value objects geométricos
de Domain antes de construir una escena.

`DiagramSceneFingerprint` calcula un SHA-256 canónico sobre metadata,
geometría, anchors, elementos y conexiones. El fingerprint es independiente
del orden de las colecciones de elementos/conexiones.

`SceneAssembly` recibe una `DrawingComposition` y una posición explícita
para cada bloque. Expande las definiciones del perfil a símbolos, primitivas,
labels, anchors y conexiones neutralizadas. No mide, no posiciona, no enruta
y no reorganiza. Si una posición falta o el perfil no coincide con el
fingerprint de la composición, falla en lugar de inventar geometría.

La frontera renderer-neutral está protegida automáticamente en dos niveles:

- referencias de assembly: Domain/Engine no pueden depender de Avalonia ni
  de `ProyectoElectrico`;
- referencias de fuente: los archivos `.cs`/`.csproj` de Domain/Engine
  se escanean en CI para impedir que esas dependencias entren de forma
  accidental.

G5 podrá construir sobre esta frontera mediante el pipeline:

```text
Measure -> Place -> Route -> Resolve -> Validate -> Scene
```

La existencia de `DiagramScene` en G4 no implica que el layout automático
esté implementado; esa responsabilidad comienza en G5.
