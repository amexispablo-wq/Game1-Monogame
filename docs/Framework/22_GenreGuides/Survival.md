# Genre Guide — Survival

## Fantasía
Recursos, craft, amenaza, noche/día, base.

## Sistemas comunes
- Needs (hunger/health/temp…)
- Inventory + crafting graphs
- World persistence (chunks)
- AI threat / spawns
- Day/night cycle
- Save frecuentes / autosave

## Arquitectura
- Streaming de mundo + budgets
- Sim tick vs presentation
- Determinismo si MP dedicated/P2P
- Proc gen seeds ([14](../14_ProceduralGeneration/README.md))

## Riesgos
- Scope infinito
- Perf en bases grandes
- Griefing si MP

## Planning checklist
- [ ] Loop 30 min “sobrevive”
- [ ] Autosave policy
- [ ] Chunk/load strategy
- [ ] MP: sí/no + autoridad
