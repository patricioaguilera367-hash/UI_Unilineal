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

``DiagramSceneFingerprint` calcula un SHA-256 canónico sobre identidad/kind,
issues, metadata, geometría, anchors, elementos y conexiones. El fingerprint
es independiente del orden de las colecciones de elementos/conexiones.

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

La existencia de `DiagramScene` en G4 no implica que el layout automático
esté implementado; esa responsabilidad comienza en G5.

## Checkpoint implementado: V1 G5

G5 implementa el layout automático determinista sin mover decisiones
eléctricas hacia la capa gráfica:

```text
SingleLineProjection
      |
      v
DrawingComposition
      |
      v
Measure
      |
      v
Place
      |
      v
Resolve structural collisions
      |
      v
SceneAssembly
      |
      v
Orthogonal routing
      |
      v
Strict validation
      |
      v
DiagramScene
```

La resolución geométrica se ejecuta antes del routing. La formulación previa
`Route -> Resolve` fue corregida durante self-review porque desplazar un
bloque después de rutear volvería obsoleta la ruta ya calculada.

`ITextMetrics` es una dependencia explícita del motor. Esto permite que
producción entregue métricas apropiadas a su backend, mientras las pruebas
usan `DeterministicTextMetrics` para obtener geometría reproducible.

Summary y board-detail tienen estrategias independientes. Summary utiliza
profundidad de topología primaria; supplies suplementarios no destruyen el
árbol base. Board-detail dispone incoming/main protection/bus/branches y
realiza wrapping por ancho configurado.

`OrthogonalConnectionRouter` produce segmentos horizontales/verticales y
evita bounds estructurales con clearance. El validator estricto vuelve a
comprobar de forma independiente que las rutas materializadas no atraviesen
bloques ajenos, de modo que un defecto del router no quede validado por
confianza circular.

`DiagramLayoutState` contiene sólo metadata de presentación. `Locked`
no se mueve; `Pinned` conserva preferencia salvo conflicto prioritario;
`Auto` queda disponible para reorganización. El estabilizador incremental
prioriza posiciones existentes y desplaza elementos nuevos antes que
perturbar geometría estable.

La escena final incorpora `SceneId` de scope, `DiagramSceneKind`,
`SceneIssue[]`, fingerprints de input/proyección/perfil y fingerprint
completo de escena. Los goldens de escena fijan estos contratos.

El gate de G5 incluye stress end-to-end de 1, 4, 12, 24, 48 y 100 circuitos,
reordenamiento equivalente de colecciones, strict validation, routing
ortogonal, architecture guards y CI Windows/Linux.

## Checkpoint implementado: V1 G6

G6 consume la escena neutral sin modificar la frontera Domain/Engine:

```text
DiagramScene
      |
      v
SceneSpatialIndex
      |
      v
Viewport
      |
      v
AvaloniaSceneRenderer
      |
      v
SingleLineView
```

`SceneSpatialIndex` indexa y consulta bounds en milímetros. El renderer
convierte el rectángulo visible desde DIPs a mm antes de consultar el índice,
de modo que el culling no introduce una segunda geometría ni contamina
`DiagramScene`.

`ViewportState` contiene únicamente zoom, pan y tamaño visible;
`ViewportController` implementa zoom bajo cursor, pan, fit, actual size y
centrado. Estas operaciones son view-only y no mutan escena, layout ni
semántica.

`HitTestIndex` reutiliza el índice espacial y convierte la tolerancia desde
DIPs según el zoom actual. La política de hit-test es independiente del
stroke técnico y resuelve ambigüedades mediante prioridad, distancia e ID
estable.

`AvaloniaSceneRenderer` es immediate mode. Renderiza únicamente candidatos
visibles con orden determinista layer/z-index/SceneId y utiliza recursos
derivados del perfil con caches acotadas. Los overlays de hover/selección se
dibujan aparte y nunca se persisten dentro de la escena.

`SingleLineView` es un único `Control` que compone viewport, índice,
hit-test, renderer y overlay pasivo. No crea un control Avalonia por cada
elemento y no contiene navegación, comandos eléctricos ni algoritmos de
layout.

La navegación Summary -> BoardDetail -> Back pertenece al shell
`UI_Unilineal.Playground`. El Playground utiliza una proyección
determinista y genera las escenas mediante `SingleLineLayoutEngine`; su
code-behind sólo cablea navegación y controles de viewport.

El gate de G6 incluye guards de dependencias, tests headless del control y
renderer, estabilidad de orden, inmutabilidad de fingerprint y un smoke
sobre una escena generada por G5 con 48 circuitos que cubre culling, render
y hit-testing. CI ejecuta restore, format, build, suite completa,
`git diff --check` y working-tree cleanliness en Windows y Linux.

La aceptación visual/runtime del Playground se mantiene deliberadamente como
paso local; los tests headless no la sustituyen.

## Checkpoint implementado: V1 G7

G7 añade la capa de interacción manteniendo separadas presentación,
renderer y autoridad eléctrica:

```text
SingleLineView
      |
      | neutral intents
      v
Playground shell
   /          \
  v            v
LayoutCommandHistory    ElectricalCommandProposal
  |                           |
  v                           v
DiagramLayoutState      IElectricalCommandHandler
  |                           |
  v                     typed CommandResult
SingleLineLayoutEngine        |
  |                     Applied only
  v                           v
DiagramScene            new SingleLineInput
                              |
                              v
                    projection -> layout -> scene
```

`InteractionStateMachine` concentra los estados Idle, Hovering, Selecting,
Panning, DraggingLayout, ConnectingElectrical, MarqueeSelecting y
CommandPreview. Navigate, Layout y Electrical son modos explícitos; cambiar
de modo o cancelar una interacción transitoria vuelve a un estado estable de
forma determinista.

Los overlays de hover, selección, marquee, drag ghost y conexión preview
siguen fuera de `DiagramScene`. `SingleLineView` traduce input Avalonia a
intents neutrales y eventos; no posee `LayoutCommandHistory`, no invoca
`IElectricalCommandHandler` y no contiene navegación ni persistencia.

Los comandos de layout operan sólo sobre `DiagramLayoutState`. Su history
local permite execute/undo/redo y cada cambio reconstruye una nueva escena
mediante `SingleLineLayoutEngine`; no modifica topología eléctrica.

La edición eléctrica comienza únicamente desde anchors semánticos en modo
Electrical. El gesto produce una `ElectricalCommandProposal` inmutable.
El shell envía un `ElectricalCommandRequest` con `ExpectedRevision` al
host y recibe `Applied`, `Rejected`, `NeedsConfirmation`, `Conflict` o
`Failed`. Sólo `Applied` reemplaza el snapshot, vuelve a proyectar y
reconstruye la escena. Rejected/Conflict conservan el diagrama canónico sin
parches optimistas.

Layout y electrical undo permanecen separados. El undo eléctrico se
representa como el comando inverso devuelto por el host y se vuelve a
ejecutar contra la revisión vigente; un estado stale puede producir
`Conflict` en lugar de forzar rollback.

`HostCapabilities` controla qué mutaciones están disponibles. El fixture
read-only prueba que navegación, selección y visualización siguen operativas
aunque Layout/Electrical estén deshabilitados.

La self-review de G7 contra §§14.4, 15, 16 y 17 confirma que la
implementación mantiene las responsabilidades allí fijadas. El hardening
incluye matriz exhaustiva pequeña de modos/eventos, cancelación de todos los
estados transitorios alcanzables, secuencia model-based de
execute/undo/redo/cancel, Rejected/Conflict sin mutación optimista y un guard
de fuente que impide que Rendering posea histories o ejecución de host.

G8 conserva la responsabilidad de `DrawingDocument`, SVG/PDF y composición
física. La integración real con `ProyectoElectrico` permanece fuera de G7.


## Checkpoint implementado: V1 G8

G8 introduce composición documental física y dos adaptadores de exportación
downstream sin alterar las responsabilidades de Domain, Engine o Rendering:

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
UI_Unilineal.Export.Svg UI_Unilineal.Export.Pdf
```

`DrawingDocument` representa hojas físicas y no deriva escala o papel del
viewport de Avalonia. Cada `DrawingSheet` conserva papel en milímetros,
orientación, escala, view box, scene viewport, márgenes, title block y
metadata de continuación.

`DocumentComposer` aplica la política de escala/legibilidad, fallback de
papel y continuaciones de forma determinista. `PrintStyleResolver`,
`DocumentPreflight` y `ExportManifest` son compartidos por formatos para
evitar que SVG y PDF inventen reglas independientes.

Los proyectos `UI_Unilineal.Export.Svg` y `UI_Unilineal.Export.Pdf`
referencian únicamente Domain + Engine. No dependen de Avalonia ni de
`ProyectoElectrico`; los guards recorren sus fuentes y proyectos para
detectar fugas. La solución incluye ambos exporters y sus proyectos de tests.

SVG y PDF serializan el mismo `DrawingDocument`. SVG conserva dimensiones
físicas/viewBox y contenido vector/text autocontenido. PDF emite páginas
multipage con MediaBox físico y operadores vectoriales/texto. No existen
rutas de screen capture, bitmap o fallback raster silencioso para primitivas
soportadas.

Los exporters de bajo nivel escriben a `Stream` y observan cancelación. El
boundary de host, demostrado en Playground, ejecuta preflight antes de tocar
el destino y usa temporal + reemplazo/move atómico para conservar el archivo
anterior ante error o cancelación.

La evidencia cross-format fija sheet count, papel, view box,
SceneViewport y fingerprints; también prueba equivalencia frente a
colecciones semánticamente reordenadas. El hardening cubre texto
Unicode/XML/PDF hostil, geometría de papel límite, cancelación y determinismo
con CI Windows/Linux.

Limitación conocida: la evidencia headless/estructural no reemplaza la
inspección visual/manual de composición, tipografía y apariencia técnica.
Ambas formas de aceptación son complementarias.

G9 conserva la responsabilidad de hardening ampliado. La integración de
lectura con `ProyectoElectrico` permanece fuera de G8 y comienza en G10.
