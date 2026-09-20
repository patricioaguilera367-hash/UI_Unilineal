# RIC 18 — contrato visual de referencia para UI_Unilineal

Fecha de captura: 2026-09-20  
Ámbito: `feature/ric18-graphic-grounding`  
Estado: referencia de diseño y pruebas; **no reemplaza el texto normativo del RIC 18**.

## 1. Propósito

Este documento fija la lectura visual que debe usar el motor de layout/render para no volver a inferir la gramática del diagrama desde capturas aisladas ni desde ajustes manuales previos.

Las fuentes usadas son complementarias:

1. **Lámina RIC 18 aportada como referencia visual/semántica.** Se usa para identificar jerarquía, sentido de lectura, ubicación relativa y comportamiento de los elementos.
2. **DXF ASCII de un calco manual de esa lámina.** Se usa para medir relaciones geométricas de un ejemplo concreto y detectar alineamientos que una imagen no permite demostrar con precisión.

### Regla de autoridad

- La lámina RIC 18 aporta la **intención gráfica y semántica**.
- El DXF aporta **evidencia geométrica medible del ejemplo**.
- Las dimensiones absolutas, offsets y proporciones del DXF **NO se consideran requisitos normativos**.
- El renderer debe convertir las relaciones estables observadas en una geometría propia, parametrizada y reproducible.

## 2. Proveniencia del ejemplo medido

DXF de referencia:

- formato: AutoCAD ASCII DXF AC1032 (2018);
- unidades declaradas: milímetros (`$INSUNITS = 4`);
- SHA-256: `1591ce0774012e009cbe2ab116fac8af7d89a70af28804c97bcfe937d48665a9`;
- entidades de modelspace: 362;
- distribución: 269 LINE, 34 MTEXT, 19 CIRCLE, 12 ARC, 12 SPLINE, 9 HATCH, 7 LWPOLYLINE;
- capa observada: `PLANO`.

Imagen RIC 18 usada para la lectura visual:

- SHA-256: `10a1adfbca0399197cc82f47480218d9ec0e05b97f7294cc5e9417b851a521ba`.

Los hashes permiten comprobar en el futuro que las mediciones de este documento corresponden exactamente a las mismas fuentes.

## 3. Invariantes visuales que sí debemos conservar

### 3.1 Jerarquía vertical

El sentido dominante del diagrama es **arriba → abajo**:

```text
EMPALME / FUENTE
       │
       │ alimentador
       │
       ▼
TABLERO / PROTECCIÓN GENERAL
       │
       ▼
BARRA PRINCIPAL
       │
       ▼
CIRCUITOS DERIVADOS
       │
       ▼
CARGAS / TABLEROS DERIVADOS
```

Un empalme de suministro no debe aparecer por debajo del tablero alimentado como resultado normal del auto-layout.

### 3.2 Eje de entrada

En el ejemplo medido:

- eje del alimentador: `X = 40.8958195809 mm`;
- centro del marco del tablero: `X = 40.8958195809 mm`.

Por tanto, el contrato del layout es:

```text
SupplyAxis.X
  = Board.CenterX
  = MainProtection.PowerAxisX
  = MainBus.IN.X
```

La coincidencia es una relación estructural; el valor `40.8958` pertenece solamente al ejemplo.

### 3.3 TP/TS y neutro

La lectura gráfica establece zonas distintas:

- TP / puesta a tierra: sector superior-izquierdo;
- N / neutro: sector superior-derecho;
- la derivación de puesta a tierra asociada al empalme aparece aguas arriba / exterior al tablero;
- N y TP no deben convertirse en un único conductor visual;
- en un circuito con diferencial, el neutro atraviesa el diferencial y TP lo evita.

### 3.4 Barra principal

La barra principal es un nivel horizontal común. Los circuitos se conectan a ella mediante nodos/taps y descienden perpendicularmente.

Los nodos de unión representados como puntos de conexión deben verse como **círculos sólidos**, no como anillos huecos.

### 3.5 Eje de cada circuito

Cada circuito mantiene un eje eléctrico vertical propio:

```text
MainBus.TAP[n].X
  = Circuit[n].PowerAxisX
  = Protection[n].PowerAxisX
  = Differential[n].PowerAxisX   (si existe)
  = Destination[n].PowerAxisX
```

El texto puede ocupar zonas auxiliares, pero no debe obligar al conductor principal a serpentear sin necesidad.

### 3.6 Distribución de N circuitos

La lámina visual contiene cuatro circuitos y el DXF medido contiene tres. Por lo tanto, la gramática debe funcionar con **N circuitos**, no depender de una cantidad fija.

En el DXF de tres circuitos, los ejes medidos son:

- `33.0555657855 mm`;
- `40.8958195809 mm`;
- `48.1087872612 mm`.

Normalizados respecto del marco del tablero (`left = 28.3500972784`, `width = 25.0914446051`):

- circuito 1: ~`0.1875`;
- circuito 2: `0.5000`;
- circuito 3: ~`0.7875`.

La observación importante no es copiar esos porcentajes: **el circuito central de un conjunto impar puede coincidir exactamente con el eje de entrada**.

Por ello, no es válido introducir un desplazamiento artificial únicamente para evitar que `MainBus.IN` y un `TAP` compartan coordenada.

Regla de layout:

- N impar: el circuito central debe poder ocupar el eje del tablero;
- N par: el eje del tablero queda entre los dos circuitos centrales;
- los circuitos deben distribuirse de manera determinista y aproximadamente simétrica;
- si dos conexiones semánticas coinciden físicamente en el mismo punto de barra, el dibujo debe emitir **un único nodo gráfico** en esa coordenada, no dos círculos superpuestos.

### 3.7 Destinos

La protección de un circuito permanece dentro del tablero. La carga final o el tablero derivado se representa como destino **fuera y aguas abajo** del marco del tablero.

Un tablero derivado no debe aparecer embebido arbitrariamente dentro del marco del tablero padre.

## 4. Mediciones del DXF: referencia, no norma

Marco principal detectado:

- izquierda: `28.3500972784 mm`;
- derecha: `53.4415418835 mm`;
- superior: `61.0405159878 mm`;
- inferior: `38.7718589007 mm`;
- ancho: `25.0914446051 mm`;
- alto: `22.2686570870 mm`;
- centro X: `40.8958195809 mm`.

Nivel principal de barra observado: aproximadamente `Y = 53.1118017161 mm`.

Estas cifras sirven para contrastar geometría y descubrir relaciones. **No deben convertirse en constantes “RIC 18” ni en límites regulatorios.**

## 5. Anti-invariantes: cosas que no debemos preservar

No se debe tratar como requisito RIC 18:

- el ancho/alto exacto del tablero del DXF;
- un pitch fijo de 17 mm entre entrada y primer circuito;
- el porcentaje exacto de altura donde quedó la barra;
- anchos de cajas de texto del calco;
- longitudes exactas de líderes o anotaciones;
- cualquier pequeño offset introducido por el trazado manual.

Un test que obligue a separar el `MainBus.IN` del circuito central sólo porque antes evitaba una superposición visual es un test de una solución accidental, no de la gramática gráfica.

## 6. Contratos verificables para el código

El auto-layout debe poder comprobar automáticamente, al menos:

```text
[PASS] Empalme/fuente está aguas arriba del tablero alimentado.
[PASS] SupplyAxis.X == Board.CenterX.
[PASS] MainProtection.PowerAxisX == SupplyAxis.X.
[PASS] MainBus.IN.X == SupplyAxis.X.
[PASS] Cada TAP coincide con el eje de su circuito.
[PASS] Protección/diferencial/destino conservan el eje del circuito.
[PASS] Para N impar, el circuito central puede coincidir con MainBus.IN.
[PASS] N está a la derecha y TP a la izquierda en la cabecera.
[PASS] Los conductores auxiliares salen de su barra antes de rutear lateralmente.
[PASS] El neutro pasa por el diferencial; TP lo evita.
[PASS] Carga/tablero derivado queda fuera y debajo del marco padre.
[PASS] Uniones físicas coincidentes generan un solo nodo gráfico sólido.
[PASS] La geometría se mantiene determinista para entrada semánticamente equivalente.
```

## 7. Uso en futuras correcciones

Antes de aplicar un hotfix visual:

1. comprobar si el defecto viola uno de estos invariantes;
2. corregir la regla de layout/composición en lugar de introducir coordenadas especiales;
3. añadir o ajustar un test del invariante;
4. regenerar los goldens sólo después de que la nueva geometría esté explicada por el contrato;
5. verificar Avalonia, SVG y PDF cuando el cambio afecte una primitiva visual.

Si una observación nueva contradice este documento, debe conservarse la evidencia nueva y actualizarse explícitamente el contrato; no se debe “arreglar a ojo” el renderer en paralelo.
