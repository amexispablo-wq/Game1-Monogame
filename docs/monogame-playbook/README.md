# MonoGame Playbook

Kit **reutilizable** para juegos nuevos en **MonoGame (DesktopGL) + .NET + Steamworks.NET**.

Extraído de patrones de **Color Blocks**. Adaptar namespaces, App ID y rutas. No copiar mecánicas de Color Blocks; copiar **estructura y checklists**.

## Cómo usar

1. Nuevo repo → seguir [`CHECKLIST-nuevo-juego.md`](CHECKLIST-nuevo-juego.md).
2. Roadmap de producto → plantilla [`02-roadmap-steam.md`](02-roadmap-steam.md).
3. Cada feature Steam/net/UI → guía numerada + ejemplo en Color Blocks (`../`).

## Índice

| Doc | Contenido |
|-----|-----------|
| [`01-bootstrap-monogame.md`](01-bootstrap-monogame.md) | Proyecto .NET, `Game` loop, escenas, tick fijo, Content |
| [`02-roadmap-steam.md`](02-roadmap-steam.md) | Plantilla de fases hasta Steam ship |
| [`03-steam-core.md`](03-steam-core.md) | Init fail-soft, callbacks, appid, nativos, Rich Presence |
| [`04-steam-party-lobby.md`](04-steam-party-lobby.md) | Lobby, invites, roster, kick/leave |
| [`05-steam-input.md`](05-steam-input.md) | Manifest VDF, live slot, fallback XInput, Partner Publish |
| [`06-networking-hostauth.md`](06-networking-hostauth.md) | Host-authoritative v1, snapshots, qué posponer |
| [`07-ui-focus-nav.md`](07-ui-focus-nav.md) | Grafo de foco, gamepad+keyboard, rebinding |
| [`08-dev-tooling.md`](08-dev-tooling.md) | developerMode, overlays, benchmarks CLI |
| [`09-steampipe-release.md`](09-steampipe-release.md) | Depots, beta branch, store checklist |
| [`CHECKLIST-nuevo-juego.md`](CHECKLIST-nuevo-juego.md) | Día 0 → first Steam build |

## Ejemplo trabajado (Color Blocks)

| Tema | Doc Color Blocks |
|------|------------------|
| Arquitectura / escenas | [`../01-ARQUITECTURA.md`](../01-ARQUITECTURA.md) |
| Coop / net | [`../03-NETWORKING-COOP.md`](../03-NETWORKING-COOP.md) |
| Steam completo | [`../05-STEAM.md`](../05-STEAM.md) |
| UI focus | [`../07-UI-NAVEGACION.md`](../07-UI-NAVEGACION.md) |
| Roadmap real | [`../08-ROADMAP.md`](../08-ROADMAP.md) |
| Dev tools | [`../09-HERRAMIENTAS-DEV.md`](../09-HERRAMIENTAS-DEV.md) |

## Principios

- **Fail-soft Steam:** juego corre sin cliente Steam (dev).
- **Un dueño por flujo:** invites/joins en un solo manager; escenas no llaman Steamworks crudo.
- **Tick fijo** para sim determinista; render desacoplado.
- **Host-auth v1** antes de predicción.
- **Partner ≠ depot:** Steam Input / Rich Presence requieren Publish en Partner, no solo archivos en build.
