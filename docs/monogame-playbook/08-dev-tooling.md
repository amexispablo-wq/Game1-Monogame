# 08 — Dev tooling

## developerMode

Flag en JSON local (`developer_settings.json`) o `#if DEBUG`:

- Muestra botones sandbox / overlays.
- No shippear `developerMode: true` en build Steam.

## Herramientas útiles

| Tipo | Para qué |
|------|----------|
| Overlay debug (F3/F8…) | Input, net, foco UI, Steam status |
| Sandbox scene | Aislar mecánica (rope, combat…) |
| Tuning panel en vivo | Tweaks sin recompilar |
| Benchmark CLI | `dotnet run -- --benchmark X` headless CI |
| Replay force-save | Regresión visual / determinismo |

## Benchmarks

- Suite de escenarios con assert pass/fail.
- Correr en cada cambio de física/core.
- No mergear si FAIL (política de equipo).

## Checklist

- [ ] Dev features gated
- [ ] Al menos un overlay de input/Steam
- [ ] Comando headless de regresión documentado
- [ ] Sandbox no reemplaza QA en niveles reales

**Referencia CB:** [`../09-HERRAMIENTAS-DEV.md`](../09-HERRAMIENTAS-DEV.md), `Developer/GameplayBenchmark/`.
