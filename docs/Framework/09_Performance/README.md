# 09 — Performance

## Presupuestos (definir por juego)

Ejemplos de targets a escribir en el README del producto:

- 60 FPS en hardware mínimo documentado
- Frame CPU &lt; 10 ms sim + 4 ms present (ajustar)
- Memoria managed estable tras warmup (sin leak por escena)

## CPU

- Evitar allocs en hot path (Update/tick): LINQ, strings, boxing.
- Pooling de listas/arrays/enemigos/proyectiles cuando el churn es alto.
- Tick fijo con cap; no simular de más en spike de frame time.

## GPU / 2D

- Atlases; menos cambios de textura.
- Menos `SpriteBatch.Begin/End`.
- Overdraw: capas opacas primero cuando importe.

## GPU / 3D

- Batching, instancing, culling, LOD.
- Resoluciones internas escalables (render scale).

## GC

- Medir picos con overlay (gen collections).
- Precargar; reutilizar buffers de IO/net.
- Cuidado con eventos que capturan y nunca unsubscriben.

## Loading

- Async + pantalla de progreso ([06](../06_AssetPipeline/README.md)).
- Streaming si el mundo es grande (survival/sandbox).
- No cargar todo el juego en el menú “por si acaso”.

## Profiling

- Usar Visual Studio / dotnet-trace / RenderDoc según plataforma.
- Guardar capturas con build id en reportes de bug.

## Checklist pre-alpha

- [ ] Overlay FPS en dev
- [ ] Cambiar escena N veces sin leak obvio
- [ ] Stress: muchos entidades / partículas
- [ ] Build Release profiled al menos una vez

Siguiente: [10_Steam](../10_Steam/README.md).
