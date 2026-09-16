# Spike preliminar del UI unilineal

## PropÃ³sito

Validar comportamiento antes de congelar el contrato real de
`UI_Unilineal.Domain`.

Este spike vive exclusivamente en `UI_Unilineal.Playground`.

## Incluye

- vista resumen jerÃ¡rquica;
- bloques de empalme y tableros;
- conexiones ortogonales;
- alimentadores ficticios;
- selecciÃ³n de tablero;
- panel de unilineal detallado;
- protecciÃ³n general;
- barra;
- circuitos/protecciones ficticias;
- `LayoutMoveCommand`;
- `ElectricalChangeSupplyCommand`;
- detecciÃ³n preliminar de ciclos.

## Modos

### PresentaciÃ³n

Arrastrar un bloque cambia solamente su posiciÃ³n ficticia.

No cambia su relaciÃ³n elÃ©ctrica.

### ElÃ©ctrico

Arrastrar un tablero sobre otro cambia su `ParentUid` ficticio.

La posiciÃ³n visual vuelve a su lugar original.

El cambio se registra como `ElectricalChangeSupplyCommand`.

## Restricciones deliberadas

- NO persiste datos.
- NO modifica ProyectoElectrico.
- NO modifica UI_Unilineal.Domain.
- NO modifica UI_Unilineal.Engine.
- NO pretende ser todavÃ­a una implementaciÃ³n fiel del RIC 18.
- Los valores elÃ©ctricos mostrados son fixtures ficticios.
- El cÃ³digo es un spike y puede descartarse completamente despuÃ©s de
  revisar la UX.

## Criterio de evaluaciÃ³n

La prueba debe responder:

1. Â¿Se entiende la diferencia entre mover visualmente y modificar
   topologÃ­a?
2. Â¿El resumen jerÃ¡rquico permite entender el proyecto?
3. Â¿Tiene sentido seleccionar un tablero y ver su detalle a la derecha?
4. Â¿La reasignaciÃ³n de alimentaciÃ³n es comprensible?
5. Â¿QuÃ© informaciÃ³n visual falta antes de diseÃ±ar el motor real?
