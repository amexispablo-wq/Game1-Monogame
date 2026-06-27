# Color Blocks — Documentación de Desarrollo

Plataformas 2D cooperativo hecho en **MonoGame (DesktopGL, .NET 9)**, con integración a **Steam** (Steamworks.NET). Aún sin lanzar. El coop **online** está diseñado a nivel de arquitectura pero todavía **no tiene transporte de red implementado** (ver `03-NETWORKING-COOP.md`).

## Índice

| Documento | Contenido |
|-----------|-----------|
| [`01-ARQUITECTURA.md`](01-ARQUITECTURA.md) | Visión general, capas, bucle de juego, estructura de carpetas, flujo de escenas |
| [`02-GAMEPLAY.md`](02-GAMEPLAY.md) | Mecánicas: colores, eyección, soga (rope), launch pads, checkpoints, meta, timer |
| [`03-NETWORKING-COOP.md`](03-NETWORKING-COOP.md) | Modelo de sesión, ownership, snapshots, input frames, estado actual del coop online y qué falta |
| [`04-NIVELES-Y-EDITOR.md`](04-NIVELES-Y-EDITOR.md) | Sistema multi-nivel, formato JSON, almacenamiento, editor de niveles |
| [`05-STEAM.md`](05-STEAM.md) | SteamManager, configuración, app id, DLLs nativas, pasos pendientes |
| [`06-GUIA-DESARROLLO.md`](06-GUIA-DESARROLLO.md) | Build, ejecución, convenciones de código, settings, debug |
| [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md) | Sistema de foco UI: grafo de navegación, gamepad/teclado/mouse, debug F8/F9, Options layout |
| [`08-ROADMAP.md`](08-ROADMAP.md) | Roadmap hacia Steam: online, leaderboards globales, Workshop, release |

> Doc legado en la raíz: `MULTI_LEVEL_SYSTEM.md` (parcialmente desactualizado; `04-NIVELES-Y-EDITOR.md` es la referencia vigente).

## Resumen ultra-rápido

- **Entry point:** `Core/Program.cs` → `Core/ColorBlocksGame.cs` (clase `Game` de MonoGame).
- **Escenas:** patrón `IScene` (Menu → Party / LevelSelect → Game / Editor / Options).
- **UI:** `UI/Navigation/` — foco por grafo explícito, rebinding en Options, debug F8/F9.
- **Party:** coop local (hasta 4) + lobby Steam (invites, roster, sync inicio de nivel).
- **Simulación:** `Core/GameSimulation.cs` corre física con **tick fijo (60 Hz)** desacoplado del render.
- **Física:** `Managers/PhysicsWorld.cs` (gravedad, colisiones AABB por color, sogas Verlet, launch pads).
- **Jugador:** `Entities/Player.cs` (movimiento, salto, eyección de plataformas del color activo).
- **Red:** `Networking/` (sesiones, ownership, snapshots, predicción) — andamiaje listo, sin sockets aún.
- **Niveles:** `LevelSystem/` + `Managers/LevelStorage.cs` (JSON en `Content/Levels/level_N.json`).
- **Steam:** `Steam/SteamManager.cs` + lobby/party (`SteamLobbyService`, `SteamPartyService`) + `steam_appid.txt`.
- **Pendiente release:** coop online (transporte), leaderboards globales, Workshop — ver [`08-ROADMAP.md`](08-ROADMAP.md).

## Convenciones clave

- Namespace único: `ColorBlocks`.
- `#nullable enable` en archivos nuevos.
- Toda la lógica de gameplay debe correr dentro del **tick fijo** de `GameSimulation`, nunca en `Draw`.
- Entidades en red implementan `INetworkEntity` y exponen `CreateSnapshot()` / `ApplySnapshot()`.
