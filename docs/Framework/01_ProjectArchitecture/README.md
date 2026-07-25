# 01 — Project Architecture

Principios que deben sobrevivir a cualquier género.

## Por qué importa

Sin separación clara: Steam se cuela en gameplay, el render decide lógica, y el multiplayer llega tarde e imposible. Diseñá **como si** hubiera red y mods aunque el v1 sea single-player.

## Game loop

```
Initialize → LoadContent → loop:
  RunPlatformCallbacks (Steam, etc.)
  SampleInput
  UpdateScene / UpdateSimulation
  Draw
Dispose
```

Un solo dueño del loop: la clase `Game`.

## Fixed simulation

- Si hay física, combate netcode, o replays: **tick fijo** (p.ej. 60 Hz) con acumulador.
- Cap de ticks/frame (anti spiral-of-death).
- **Regla de oro:** lógica determinista solo en el tick; `Draw` no muta sim.

## Scene management

- Interfaz `IScene`: `Update`, `Draw`, `OnExit`.
- `ChangeScene` limpia foco/input residual (suppress confirm hasta release).
- Modales globales (invite, crash dialog) pueden vivir en el host, no en cada escena.

## Rendering vs gameplay

| Capa | Responsabilidad |
|------|-----------------|
| Simulation | Estado, reglas, timers, AI tick |
| Presentation | Cámara, interpolación visual, UI, VFX |
| Platform | Steam, filesystem, audio device |

Gameplay **no** llama APIs de Steam/filesystem directo; usa servicios.

## Service architecture

- Servicios creados en el host (`Game` ctor / Initialize).
- Escenas reciben el host o interfaces (`ISettings`, `ISaveStore`).
- Preferí interfaces en bordes (Steam, net, save) para tests y fail-soft.

## Ownership y SRP

- Un dueño por flujo (ej. invites/joins → un `InviteManager`).
- Un dueño por recurso mutable (lobby, save slot activo).
- Evitar “managers dios”; partir por dominio.

## Eventos

- Callbacks de plataforma → eventos C# en un CallbackManager.
- Gameplay puede usar eventos internos; documentar quién subscribe/unsubscribe en `OnExit`.

## Loose coupling / scalability

- Datos (JSON/binarios versionados) desacoplados de UI.
- Content oficial ≠ UserData ≠ UGC.
- Diseñá IDs y ownership pensando en multiplayer ([11](../11_Multiplayer/README.md)).
- Diseñá paths y manifests pensando en Steam/Workshop ([10](../10_Steam/README.md)).
- Hooks de modding futuros: no hardcodear rutas absolutas; cargar por ID.

## Compatibilidad futura

| Objetivo | Decisión temprana |
|----------|------------------|
| Multiplayer | Ownership en entidades; input por frame; no RNG sin seed |
| Steam | Fail-soft; un wrapper; presencia/lobby fuera de Entities |
| Modding | Formatos versionados; content packs por carpeta/ID |

Ver también [20_GameStandards](../20_GameStandards/README.md).
