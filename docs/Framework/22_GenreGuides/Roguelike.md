# Genre Guide — Roguelike / Roguelite

## Fantasía
Runs, RNG, meta-progresión opcional, muerte con consecuencias.

## Sistemas comunes
- Run seed + genVersion
- RNG encapsulado
- Meta-save vs run-save
- Relics/items data-driven
- Death / unlock flow
- Balance logs

## Arquitectura
- Proc gen determinista ([14](../14_ProceduralGeneration/README.md))
- Replay opcional de runs
- Separar unlock meta de inventario de run

## Riesgos
- RNG sense of unfair
- Seed instability tras patch
- Scope de sinergias items

## Planning checklist
- [ ] Seed shown in UI
- [ ] Meta progression map
- [ ] One complete act loop
- [ ] Patch policy for seeds
