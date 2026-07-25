# 13 — AI (starter)

Guía genérica. No impone behavior trees vs GOAP; impone **presupuesto y determinismo**.

## Principios

1. AI corre en **tick de sim** (o substep documentado), no en Draw.
2. **Seedable** si afecta multiplayer o replays.
3. Presupuesto: N agents/tick o time slice; no spike de frame.
4. Datos (params) en content versionado; código = interpreter.

## Building blocks comunes

| Pieza | Uso |
|-------|-----|
| Blackboard / world queries | Compartir percepción |
| Finite State Machine | Enemies simples |
| Behavior Tree / Utility | Complejidad media |
| Pathfinding (grid/navmesh) | Movimiento |
| Perception (FOV, hearing) | Awareness |

## Multiplayer

- Host corre AI en host-auth v1.
- No confiar en AI del cliente.
- Sync resultado (posiciones/estados), no “pensamientos” crudos si el bandwidth duele.

## Debugging

- Overlay: estado actual, target, path.
- Gizmos de FOV/path en DeveloperMode.
- Log de transiciones (rate-limited).

## Checklist starter

- [ ] Un enemy/NPC de referencia con FSM
- [ ] Params en data, no magic numbers en código
- [ ] Debug draw gated
- [ ] Decisión documentada: AI solo host / o shared sim

Ver [01_ProjectArchitecture](../01_ProjectArchitecture/README.md), [11_Multiplayer](../11_Multiplayer/README.md).
