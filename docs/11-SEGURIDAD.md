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
| `LeaderboardSanity` | Bloquea **upload** si tiempo absurdo / replay mismatch / wall-clock extremo / timer freeze | No verifica scores ajenos en servidor; **no** mira velocidad/salto |
| `ActiveWallClock` | Mide wall-clock solo con foco + timer + unpaused; va a replay metadata | No cambia física ni tick rate |
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
2. **Wall-clock** (`ActiveWallSeconds` en metadata): si `> 0`, ratio `wall/score` debe estar en `[0.65 .. 1.50]`. Fuera = speedhack. `0` (legacy) → **fail-open** (no bloquea).
3. **Timer integrity** sobre frames trimmeados: `ElapsedTime` no decrece mientras corre; ≥30 frames con elapsed congelado **y** jugador moviéndose → rechazo (freeze timescore).

**Fail = solo skip Steam upload.** PB local y replay se conservan. Gameplay en vivo no se altera.

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
- Wall-clock y freeze detectan CE básico en upload; no sustituyen re-sim server-side.
