# Genre Guide — Platformer

## Fantasía
Movimiento preciso, niveles, checkpoints, timers opcionales.

## Sistemas comunes
- Controller feel (coyote, buffer jump)
- Tile/collision o hitboxes
- Checkpoints / respawn
- Camera follow + look-ahead
- Level pipeline + editor
- Parallax / layers (presentación)

## Arquitectura
- Tick fijo si hay net coop o replays
- Separar “character controller” de render de animaciones
- Level data versionado

## Riesgos
- Feel interminable sin sandbox de movimiento
- Camera nausea
- Coop desync si no hay autoridad clara

## Planning checklist
- [ ] Vertical slice 1 nivel “feel final”
- [ ] Muerte/respawn documentado
- [ ] Editor o pipeline de niveles
- [ ] Decisión: timer / lives / assist options
