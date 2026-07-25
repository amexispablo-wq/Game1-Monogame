# Genre Guide — RPG

## Fantasía
Progresión, inventario, combate, quests, diálogo.

## Sistemas comunes
- Stats / inventory / equipment
- Save slots robustos + migración
- Dialogue / quest graph
- Combat (turn/RT) como módulo
- World map / scenes streaming
- UI densa (inventario) con focus pad

## Arquitectura
- Data-driven items/quests (JSON/Scriptable)
- IDs estables de items
- No hardcodear economía en UI
- Considerá MP solo si el diseño lo pide desde día 1

## Riesgos
- Scope de contenido
- Saves rotos mid-act
- UI pad en grids de inventario

## Planning checklist
- [ ] Combat vertical slice
- [ ] Inventory UX pad-ready
- [ ] Quest “critical path” data format
- [ ] Save migration strategy
