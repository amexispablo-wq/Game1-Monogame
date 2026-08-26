# Seguridad — Color Blocks

Modelo de confianza y controles client-side. **No hay anticheat de servidor**: Steam leaderboards siguen siendo honesty-based frente a un cliente parchado.

## Qué protege

| Capa | Qué hace | Qué no hace |
|------|----------|-------------|
| `LevelValidator` | Bounds, counts, strings — bloquea grief/crash JSON | No firma contenido |
| `OfficialLevelManifest` | SHA256 de oficiales vs `manifest.json` — **rechaza** carga si alterados | Bypass en Developer Mode; cliente parchado puede saltarlo |
| `SaveIntegrity` (HMAC) | BestTimes firmados; edición notepad → rechazo | No resiste reverse engineering |
| `AtomicFileWriter` | Evita JSON a medias si crash mid-write | — |
| Workshop gates | Strict validate + PNG magic + skip `.exe`/`.dll` en UGC | Steam puede hostear extras; juego no los ejecuta |
| `TrustedFrameClock` | Dt multi-fuente: max de **totales** QPC/TickCount/Utc (anti 2× por cuantización) | CE que hookea *todas* las APIs de tiempo; cliente parchado |
| `LeaderboardSanity` | Bloquea **upload** si tiempo absurdo / replay mismatch / wall-clock extremo / timer freeze | No verifica scores ajenos en servidor; **no** mira velocidad/salto |
| `ActiveWallClock` | Acumula deltas trusted solo con foco + timer + unpaused; va a replay metadata | No cambia física ni tick rate |
| Replay `DataChecksum` | Detecta edición de frames locales | WR ajenos pueden keep-on-mismatch |

## Oficiales alterados

1. Build genera `Content/OfficialLevels/manifest.json` (`BuildTools/Generate-OfficialManifest.ps1`).
2. `LevelLibrary.LoadLevel` verifica hash.
3. Fail → `LevelIntegrityException` → UI: *Verify integrity of game files in Steam*.

## BestTimes

Envelope:

```json
{ "v": 1, "payload": { ... }, "hmac": "HEX" }
```

Clave = salt embebido + `%LocalAppData%\Color Blocks` + `.save_salt` + SteamID (si hay). Legacy sin firma se lee una vez y se re-firma al guardar.

Sanity: tiempos fuera de `[0.01s .. 86400s]` se rechazan. `< 0.5s` se guarda local pero **no** sube a Steam.

## Workshop

- Publish: solo Local, Strict validate, max 2 MB, staging = solo `level.json`.
- Preview: magic `89 50 4E 47`, 16 B–4 MB.
- Download: Strict validate; si hay `.exe`/`.dll`/… en carpeta UGC → skip sync.

## Leaderboards

Antes de `UploadRecord` (`LeaderboardSanity.TryValidateUpload`):

1. Tiempo bounds, replay existe, `DataChecksum` OK, `|duration − score| < 0.05s`, level hash match, oficial intacto, player count match.
2. **Wall-clock** (`ActiveWallSeconds` en metadata): ratio `wall/score` debe estar en `[0.85 .. 1.25]`. Fuera = speedhack. `≤ 0` (legacy / ausente) → **fail-closed** (no sube).
3. **Timer integrity** sobre frames trimmeados: `ElapsedTime` no decrece mientras corre; ≥30 frames con elapsed congelado **y** jugador moviéndose → rechazo (freeze timescore).

**Fail = solo skip Steam upload.** PB local y replay se conservan. Gameplay en vivo no se altera por el gate de upload.

### Anti-speedhack (CE)

- **Local:** `GameScene` avanza la sim con `TrustedFrameClock` (no solo `ElapsedGameTime` / QPC). Cada fuente (QPC / TickCount64 / UtcNow) se integra **por separado**; el dt entregado sigue el max de totales acumulados. **No** hacer `max(dTick,dUtc)` por frame: cuantización desfasada ≈ 2× a FPS altos. UI/overlays siguen con MonoGame frame time. Si CE solo frena QPC, totales wall tiran el target → no slow-mo útil.
- **Upload:** `ActiveWallClock` suma los mismos deltas trusted (pausa en focus loss / pause / death / spawn-hold / photo / client). Ratio estricto + fail-closed.
- **Residual:** hook de *todas* las fuentes de tiempo, binario que salta `LeaderboardSanity`, o upload directo vía Steam API. Sin backend no hay trust-root.

### Qué deliberadamente no se chequea

- Caps de velocidad / salto / gravedad / rope slingshot / launch / speed buff.
- Física legítima supera `MaxMoveSpeed` (lanzadores, ropes) → un cap de vel = falso positivo inaceptable.
- Scan de Cheat Engine / WeMod, kicks mid-run, VAC.

Entries remotas con details vacíos / tiempos absurdos → `IsSuspicious` (UI `!` + tint); no se borran.

Benchmark: `security.leaderboard_sanity` en GameplayBenchmark.

## Multiplayer

Host chequea manifest oficial antes de `BroadcastLevelStart`. Clients ya tienen build + level hash gate.

## Residual (sin backend)

- Cliente modificado / memory editor / Steam API directa.
- HMAC disuade edición manual, no atacante determinado.
- Leaderboards globales no son trust-root.
- Multi-fuente + ratio estrecho suben la barra frente a CE básico; no sustituyen re-sim server-side.
