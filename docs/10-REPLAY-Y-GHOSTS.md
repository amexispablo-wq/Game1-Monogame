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
- Por board de player-count: pide top LB → si cambia handle/score, re-descarga WR.
- Cache WR: `%LocalAppData%/…/Ghosts/{Official|Workshop}/…` + sidecar meta (UGC, version, score).
- `EnsureEntryReplay` — download/cache de **cualquier** fila LB (`Cache/Replays/.../{level}_p{n}_{ugc}.replay`). LRU cap **5** archivos (borra oldest por LastAccessTime). No toca PB locales ni Ghosts WR.
- Playback: `GhostPlayer` / `ReplayViewerScene` (mismo formato).

## Escenas / UX

| Pieza | Uso |
|-------|-----|
| `ReplayViewerScene` | Best local, path WR, o path de fila LB; `createReturnScene` vuelve a Leaderboard |
| `LeaderboardScene` | Columna Replay por fila (1–4P board); sticky PB si off-screen |
| Level Select | Watch Replay / Watch WR (aún usan party size) |
| Menú | `ReplayBackgroundRenderer` si hay replay + toggle |

## Dev keys

Ver [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md): F10 force-save / F11 background. Requiere `developerMode` para varios atajos.

## Relación con Leaderboards

1. Completar nivel → mejor tiempo local opcional.
2. Share replay → UGC handle (cada PB KeepBest, no solo WR).
3. Upload score LB con details + attach UGC.
4. Leaderboard fila → `EnsureEntryReplay` → `ReplayViewerScene`.
5. WR in-game ghost: top LB → `EnsureWorldRecordGhost` → `GhostPlayer`.

### Replay ausente

- Fila con `--` o download fail: entry sin `ReplayId`/`GhostId` (Cloud share falló al subir, o Attach no corrió porque KeepBest no cambió).

Detalle boards: [`05-STEAM.md`](05-STEAM.md). Roadmap polish: [`08-ROADMAP.md`](08-ROADMAP.md) Fase 3.
