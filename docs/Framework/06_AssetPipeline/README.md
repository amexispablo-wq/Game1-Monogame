# 06 — Asset Pipeline

## Official vs user vs UGC

| Fuente | Editable en runtime | Ship |
|--------|---------------------|------|
| Official | Solo tools/dev | Sí (Content/) |
| User / Local | Sí | No |
| Workshop / Mod | Read-only o copy-on-write | No |

Misma idea en cualquier género: **no mezclar** packs de fábrica con suscripciones.

## MonoGame Content

- `Content.mgcb` para assets que necesitan procesado (textures, fonts, FBX…).
- Loose files (JSON niveles, configs) con `CopyToOutputDirectory` si no pasan por MGCB.
- Documentar en README del juego qué va por cada camino.

## Async loading

- Pantallas de loading / progress para niveles grandes o 3D.
- No bloquear el UI thread con IO pesado sin feedback.
- Cancelación al cambiar de escena.
- Precarga opcional de menú (música, UI atlas).

## Naming y organización

```
Content/
  Textures/
  Audio/
  Data/          # oficial
UserData/
  Saves/
  UserContent/
  Cache/
  Logs/
```

## Hot-reload (dev)

Opcional en DeveloperMode: recargar JSON/tuneables sin reiniciar. Nunca requerido en ship build.

## Pitfalls

- Artistas escriben en `bin/Debug/...` y pierden trabajo al clean.
- Paths case-sensitive rompen en Linux/Deck si desarrollás solo en Windows.
- Duplicar el mismo asset en 10 tamaños sin atlas.

Ver [09_Performance](../09_Performance/README.md), [15_ReleasePipeline](../15_ReleasePipeline/README.md).
