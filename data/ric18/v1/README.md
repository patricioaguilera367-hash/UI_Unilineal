# RIC18 Drawing Profile v1

This directory contains the renderer-neutral drawing profile consumed by
UI_Unilineal.

## Provenance rule

Every graphic rule declares one classification:

- `RIC18_EXPLICIT`
- `RIC18_CATALOG`
- `RIC18_REFERENCE`
- `APP_CONVENTION`

The current v1 seed geometry, styles, blocks and layout values are all
`APP_CONVENTION`. They are application drawing conventions and make no
claim that their exact geometry, dimensions or spacing are mandated by
RIC N°18.

A rule may only be promoted to a RIC classification after an authoritative
source document and exact locator have been recorded and accepted by
`DrawingProfileValidator`.
