# 14 — Procedural Generation (starter)

## Principios

1. **Seed** explícito; mismo seed ⇒ mismo resultado en la misma versión de generador.
2. Versioná el generador (`genVersion`); seeds viejos pueden invalidarse — documentalo.
3. Separá **layout** de **población** (items/enemigos) si querés mezclar handcrafted + proc.
4. Multiplayer: **un seed de sesión**; todos generan igual o solo host genera y replica.

## Pipeline típico

```
Seed → Topology/Layout → Critical path validation → Populate → Polish passes
```

Fallá temprano si no hay ruta/objetivo (según género).

## Contenido

- Tablas de peso en data (JSON).
- Budgets (rooms, enemies, loot) configurables.
- Biomes/tags como filtros, no ifs eternos en código.

## Determinismo

- RNG encapsulado por sistema (`System.Random` con seed o PRNG propio).
- No uses `Random.Shared` mezclado con orden de update no determinista.
- Cuidado con iterar `Dictionary` sin orden estable.

## Debugging

- Mostrar seed en pause/overlay.
- Dump layout a imagen/JSON en DeveloperMode.
- Reproducir bug = “seed + genVersion + build”.

## Checklist

- [ ] Seed en UI o log de sesión
- [ ] genVersion en save
- [ ] Validación de jugabilidad mínima
- [ ] Política MP documentada

Ver [05_SaveSystem](../05_SaveSystem/README.md), [22_GenreGuides](../22_GenreGuides/README.md).
