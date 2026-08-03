# Color Blocks — Documentación de Desarrollo

Plataformas 2D cooperativo hecho en **MonoGame (DesktopGL, .NET 9)**, con integración a **Steam** (Steamworks.NET). Aún sin lanzar. Coop **online v1** (transporte Steam + snapshots host-authoritative) **implementado**; falta predicción, interpolación y QA 2-clientes (ver `03-NETWORKING-COOP.md`).

Leaderboards, Workshop, Replay/Ghost y prompt in-game de invites **existen en código**; el trabajo abierto es QA, Partner config y release (ver `05-STEAM.md`, `08-ROADMAP.md`).

---

## Framework MonoGame (juegos nuevos)

Kit **canónico, agnóstico al género** (2D/3D, platformer/RPG/survival/…). No es documentación de Color Blocks.

→ **[`Framework/README.md`](Framework/README.md)**

Empezá por [`20_GameStandards`](Framework/20_GameStandards/README.md) + [`00_GettingStarted`](Framework/00_GettingStarted/README.md).

> Legado: [`monogame-playbook/`](monogame-playbook/README.md) redirige aquí.

---

## Índice — Color Blocks (producto)

| Documento | Contenido |
|-----------|-----------|
| [`01-ARQUITECTURA.md`](01-ARQUITECTURA.md) | Visión general, capas, bucle, carpetas, escenas, sistemas secundarios |
| [`02-GAMEPLAY.md`](02-GAMEPLAY.md) | Mecánicas CB: colores, soga, pads, checkpoints, timer |
| [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md) | Sesión, ownership, snapshots, coop online |
| [`04-NIVELES-Y-EDITOR.md`](04-NIVELES-Y-EDITOR.md) | `LevelLibrary`, Official/Local/Workshop, editor |
| [`05-STEAM.md`](05-STEAM.md) | Steam producto: lobby, invites, Input, LB, Workshop, Ghost |
| [`06-GUIA-DESARROLLO.md`](06-GUIA-DESARROLLO.md) | Build, convenciones, deuda |
| [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md) | Foco UI, rebinding, debug F8/F9 |
| [`08-ROADMAP.md`](08-ROADMAP.md) | Roadmap Steam CB |
| [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md) | Dev mode, sandbox, benchmarks |
| [`10-REPLAY-Y-GHOSTS.md`](10-REPLAY-Y-GHOSTS.md) | Replay pipeline + Steam Ghost |
| [`11-SEGURIDAD.md`](11-SEGURIDAD.md) | Seguridad cliente, residual sin anticheat server |
| [`12-LEADERBOARD-ADMIN.md`](12-LEADERBOARD-ADMIN.md) | Cupo LB Steam, top workshop UGC, tool local de maintenance |
| [`STEAM_INPUT_OFFICIAL_SHIP.md`](STEAM_INPUT_OFFICIAL_SHIP.md) | Checklist Partner Gamepad Publish |

> Doc legado en la raíz: `MULTI_LEVEL_SYSTEM.md` (usar `04-NIVELES-Y-EDITOR.md`).

## Resumen ultra-rápido (Color Blocks)

- **Entry:** `Core/Program.cs` → `ColorBlocksGame`.
- **Escenas:** Menu, Party, LevelSelect, Game, Editor, Options, Customization, Leaderboard, ReplayViewer, RopeSandbox.
- **Party / Steam:** lobby, invites in-game, Kick/Leave, LB, Workshop, Replay/Ghost.
- **Sim:** tick fijo 60 Hz en `GameSimulation`.
- **Niveles:** `LevelLibrary` — Official / LocalAppData User + Workshop.
- **Greenfield:** no copies mecánicas CB — usá [`Framework/`](Framework/README.md).

## Convenciones clave (producto)

- Namespace `ColorBlocks`, `#nullable enable` en código nuevo.
- Gameplay en tick fijo; no en `Draw`.
- Entidades net: `INetworkEntity` + snapshots.
