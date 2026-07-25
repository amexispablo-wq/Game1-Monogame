# 10 — Replay y Ghosts

Pipeline de grabación, reproducción local, highlights de menú, share Steam UGC y ghosts de World Record.

## Vista rápida

```
GameScene (run)
  → ReplayRecorder → .replay local (ReplayStorage)
  → al completar: SteamReplayService.ShareReplayFile → UGC handle
  → SteamLeaderboardService upload + AttachLeaderboardUGC

SteamGhostService
  → top LB entry → download UGC vía SteamReplayService
  → cache Ghosts/{Official|Workshop}/{levelId}_WorldRecord_p{n}.replay
  → GhostPlayer / ReplayViewerScene
```

## Carpetas clave — `Replay/`

| Tipo | Rol |
|------|-----|
| `ReplayData` / `ReplayFrame` / `ReplayMetadata` | Formato en memoria + meta (level hash, players) |
| `ReplayRecorder` | Graba durante `GameScene` |
| `ReplayPlayer` | Reproduce frames en viewer / ghost |
| `ReplayStorage` / `ReplayFileSerializer` | Persistencia `.replay` JSON |
| `ReplayManager` | Último replay en memoria + menú background toggle |
| `GhostPlayer` / `GhostMode` | Ghost in-game (WR u otro) |
| `ReplayWorld` | Mundo mínimo para dibujar replay |
| `HighlightManager` / `HighlightEventDetector` / clips | Highlights menú / composite |
| `ReplayBackgroundRenderer` | Fondo animado en menús |
| `ReplayDiagnostics` / `ReplayDebugOverlay` | Debug F10/F11 |

Namespace: `ColorBlocks.Replay` (salvo servicios Steam en `ColorBlocks`).

## Steam — `SteamReplayService`

- Sube/descarga archivos `.replay` vía **Steam Remote Storage UGC**.
- Handle UGC = `ReplayId` / `GhostId` en filas de leaderboard.
- Gameplay no llama directo: `GameScene` (upload post-run) y `SteamGhostService` (download).

## Steam — `SteamGhostService`

- Solo Official + Workshop (`SupportsWorldRecordGhost`; Local no).
- Por board de player-count: pide top LB → si cambia handle/score, re-descarga.
- Cache: `%LocalAppData%/…/Ghosts/{Official|Workshop}/…` + sidecar meta (UGC, version, score).
- Playback: `GhostPlayer` (mismo formato que replay local).

## Escenas / UX

| Pieza | Uso |
|-------|-----|
| `ReplayViewerScene` | Ver best local o path WR pasado desde Level Select |
| Level Select | Descargar / abrir world record replay |
| Menú | `ReplayBackgroundRenderer` si hay replay + toggle |

## Dev keys

Ver [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md): F10 force-save / F11 background. Requiere `developerMode` para varios atajos.

## Relación con Leaderboards

1. Completar nivel → mejor tiempo local opcional.
2. Share replay → UGC handle.
3. Upload score LB con details + attach UGC.
4. Otros clientes: LB top → GhostService cache → ghost race / viewer.

Detalle boards: [`05-STEAM.md`](05-STEAM.md). Roadmap polish: [`08-ROADMAP.md`](08-ROADMAP.md) Fase 3.
