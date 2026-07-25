# Color Blocks — Documentación de Desarrollo

Plataformas 2D cooperativo hecho en **MonoGame (DesktopGL, .NET 9)**, con integración a **Steam** (Steamworks.NET). Aún sin lanzar. Coop **online v1** (transporte Steam + snapshots host-authoritative) **implementado**; falta predicción, interpolación y QA 2-clientes (ver `03-NETWORKING-COOP.md`).

Leaderboards, Workshop, Replay/Ghost y prompt in-game de invites **existen en código**; el trabajo abierto es QA, Partner config y release (ver `05-STEAM.md`, `08-ROADMAP.md`).

## Índice — Color Blocks

| Documento | Contenido |
|-----------|-----------|
| [`01-ARQUITECTURA.md`](01-ARQUITECTURA.md) | Visión general, capas, bucle de juego, estructura de carpetas, flujo de escenas |
| [`02-GAMEPLAY.md`](02-GAMEPLAY.md) | Mecánicas: colores, eyección, soga (rope), launch pads, checkpoints, meta, timer |
| [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md) | Modelo de sesión, ownership, snapshots, input frames, estado actual del coop online y qué falta |
| [`04-NIVELES-Y-EDITOR.md`](04-NIVELES-Y-EDITOR.md) | `LevelLibrary`, fuentes Official/Local/Workshop, formato JSON, editor |
| [`05-STEAM.md`](05-STEAM.md) | SteamManager, lobby, invites, Input, Leaderboards, Workshop, Ghost/Replay |
| [`06-GUIA-DESARROLLO.md`](06-GUIA-DESARROLLO.md) | Build, ejecución, convenciones de código, settings, debug |
| [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md) | Sistema de foco UI: grafo de navegación, gamepad/teclado/mouse, debug F8/F9 |
| [`08-ROADMAP.md`](08-ROADMAP.md) | Roadmap hacia Steam: online QA, polish LB/Workshop, SteamPipe, release |
| [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md) | Dev mode, Rope Sandbox, tuning F6, benchmarks, replay debug |
| [`10-REPLAY-Y-GHOSTS.md`](10-REPLAY-Y-GHOSTS.md) | Pipeline Replay/, SteamReplayService, ghosts, ReplayViewerScene |
| [`STEAM_INPUT_OFFICIAL_SHIP.md`](STEAM_INPUT_OFFICIAL_SHIP.md) | Checklist Partner: template Gamepad → Publish |

> Doc legado en la raíz: `MULTI_LEVEL_SYSTEM.md` (parcialmente desactualizado; `04-NIVELES-Y-EDITOR.md` es la referencia vigente).

## Playbook MonoGame (reutilizable)

Kit **agnóstico al juego** para próximos títulos MonoGame + Steam. Extraído de patrones de Color Blocks.

→ **[`monogame-playbook/README.md`](monogame-playbook/README.md)**

Incluye bootstrap, plantilla de roadmap, Steam (core / lobby / input), host-auth net, UI focus, tooling y checklist día-0 → first Steam build.

## Resumen ultra-rápido

- **Entry point:** `Core/Program.cs` → `Core/ColorBlocksGame.cs` (clase `Game` de MonoGame).
- **Escenas:** patrón `IScene` — Menu, Party, LevelSelect, Game, Editor, Options, Customization, Leaderboard, ReplayViewer, RopeSandbox (dev).
- **UI:** `UI/Navigation/` — foco por grafo explícito, rebinding en Options, debug F8/F9.
- **Party:** coop local (hasta 4) + lobby Steam (invites overlay + prompt Accept/Decline in-game, roster, kick/Leave).
- **Simulación:** `Core/GameSimulation.cs` corre física con **tick fijo (60 Hz)** desacoplado del render.
- **Física:** `Managers/PhysicsWorld.cs` (gravedad, colisiones AABB por color, sogas Verlet, launch pads).
- **Jugador:** `Entities/Player.cs` (movimiento, salto, eyección de plataformas del color activo).
- **Red:** `Networking/` + `SteamGameNetworkService` — transporte + snapshots v1; predicción pendiente.
- **Niveles:** `LevelSystem/LevelLibrary.cs` — Official (`Content/OfficialLevels/`), Local (`%LocalAppData%/…/UserLevels/`), Workshop (`%LocalAppData%/…/Workshop/`).
- **Steam:** lobby/party/invites + Input + Leaderboards + Workshop + Replay/Ghost UGC.
- **Replay:** `Replay/` + `SteamReplayService` / `SteamGhostService` — ver doc 10.
- **Dev:** `Developer/GameplayBenchmark/`, `RopeSandboxScene`, `GameplayTuning` — ver doc 09.
- **Pendiente release:** QA online (pred/interp), Partner tokens/Steam Input Publish, SteamPipe — ver [`08-ROADMAP.md`](08-ROADMAP.md).
- **Rope:** rewrite Verlet 2026; regresiones → `--benchmark rope`.

## Convenciones clave

- Namespace único: `ColorBlocks`.
- `#nullable enable` en archivos nuevos.
- Toda la lógica de gameplay debe correr dentro del **tick fijo** de `GameSimulation`, nunca en `Draw`.
- Entidades en red implementan `INetworkEntity` y exponen `CreateSnapshot()` / `ApplySnapshot()`.
- Juego nuevo / greenfield: empezar por [`monogame-playbook/`](monogame-playbook/README.md).
