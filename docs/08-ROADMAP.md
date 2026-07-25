# 08 — Roadmap hacia Steam

Visión del producto: lanzamiento en **Steam** con coop **local y online**, **highscores globales** por nivel, y **Steam Workshop** para niveles de la comunidad.

Este documento resume estado actual vs. pasos sugeridos. No es un compromiso de fechas.

---

## Estado actual (resumen)

| Área | Estado |
|------|--------|
| Gameplay local (1–4 jugadores) | ✅ Funcional |
| Física de soga (rewrite Verlet 2026) | ✅ Funcional; regresiones en benchmark |
| Dev: Rope Sandbox + tuning F6 + benchmarks | ✅ Funcional (requiere `developerMode`) |
| Editor de niveles | ✅ Funcional (Local; Official en dev) |
| Mejores tiempos locales | ✅ `BestTimeStorage` |
| UI / navegación gamepad+teclado+mouse | ✅ Grafo explícito, debug F8/F9 |
| Rebinding teclado + gamepad | ✅ Options; verificar persistencia gamepad |
| Coop local (teclado + gamepads) | ✅ `PartyManager`, `PartyScene` |
| Steam init + callbacks | ✅ `SteamManager` |
| Steam lobby + invitaciones + roster | ✅ Lobby/Party/Invite (+ prompt in-game) |
| Sync inicio de nivel vía lobby | ✅ Líder elige nivel, todos cargan |
| Coop online (simulación sincronizada) | 🟡 v1 host-authoritative; falta predicción + QA |
| Highscores globales (Steam Leaderboards) | ✅ Código (`SteamLeaderboardService` + UI); QA/Partner |
| Steam Workshop (UGC) | ✅ Código (`SteamWorkshopService` + `LevelLibrary`); QA/Partner |
| Replay / Ghost WR | ✅ `SteamReplayService` / `SteamGhostService` — doc 10 |
| Achievements / cloud saves | ❌ No implementado |
| Empaquetado SteamPipe / depots | 🟡 Scripts parciales; ship Partner pendiente |

---

## Fase 0 — Rope + QA dev (ahora)

Objetivo: soga estable en niveles reales antes de más features Steam.

| Item | Estado |
|------|--------|
| Rewrite Verlet + stretch-only constraints | ✅ |
| Pull → acortar rest length + tensión al otro PJ | ✅ |
| Colored collision + color mix extremos (R+G=amarillo) | ✅ |
| Fix input sandbox (`GameplayInputBlocked`) | ✅ |
| Benchmark suite rope (14 mecánicas × 2 modos) | ✅ |
| Feel tuning en niveles diseñados (no solo sandbox) | 🟡 Manual QA pendiente |
| Sandbox con selector `ColoredPhysics` | ❌ Opcional |
| Fuzz testing estable en CI | ❌ Opcional |

**Comando regresión:** `dotnet run -- --benchmark rope` (ver [`09-HERRAMIENTAS-DEV.md`](09-HERRAMIENTAS-DEV.md)).

---

## Fase 1 — Pulido pre-Steam (corto plazo)

Objetivo: experiencia sólida en single-player y coop local.

1. **Fix persistencia gamepad bindings** — `SettingsManager` / `GamepadBindings`.
2. **Completar flags de nivel** — `LavaRise`, `Player1..4`, modos de soga end-to-end.
3. **Audio** — confirmar `MusicVolume` de Options afecta al motor.
4. **QA resoluciones** — Options responsive 720p–3440×1440.
5. **Niveles oficiales versionados** — `Content/OfficialLevels/` en source (ya); no perder al clean.
6. **`.gitignore`** — excluir `bin/`, `obj/` si aún entran.
7. **Rich presence Partner** — tokens `#StatusInParty` etc. desde `Steam/rich_presence_english.txt`.

---

## Fase 2 — Coop online (en progreso)

**v1 scaffold listo.** Lobby + transporte + loop host-authoritative. Falta QA 2-client, predicción, interpolación.

### 2.1–2.3 Hecho

- `SteamGameNetworkService` — `ISteamNetworkingMessages`, canales 0/1.
- `NetworkPacketCodec` — `InputFrame` + `GameSnapshot`.
- `GameSession.CreateOnline`, roster `OwnerId` / `NetworkPlayerId`.
- Loop: Host PumpIncoming → Advance → BroadcastSnapshot; Client SendLocalInput → ApplySnapshot.

### 2.4 Gameplay online — pendiente

- Interpolación entre snapshots.
- Predicción client-side (opcional v1.1).
- Desconexión parcial (`MemberLeft` → PartyScene).
- F3 debug: rol NET + snapshot seq.

### 2.5 QA online — pendiente

- 2 Steam clients, latencia, host migration (opcional v1).

---

## Fase 3 — Leaderboards (código listo → polish)

Objetivo: ranking confiable en store / marketing. **API + UI ya existen.**

### Hecho

- `SteamLeaderboardService` — boards `{levelId}_v{ver}_p{n}`, upload al completar, details + UGC replay.
- `LeaderboardScene` + hooks Level Select / post-run.
- Ghost WR vía `SteamGhostService` (doc 10).

### Pendiente (polish)

1. Crear/verificar boards en Steam Partner (nombres alineados al código).
2. QA upload/download Official + Workshop; offline fallback (solo local).
3. UX: feedback “Nuevo récord global #N” si falta.
4. Anti-cheat mínimo: documentar confianza single-player; host-auth en online.

---

## Fase 4 — Workshop (código listo → polish)

Objetivo: comunidad publica y juega niveles. **UGC sync ya existe.**

### Hecho

- `SteamWorkshopService` — publish Local, sync subs → `%LocalAppData%/…/Workshop/{id}/`.
- `LevelLibrary` lista Workshop read-only; Duplicate → Local.
- Leaderboards Workshop vía mismo servicio LB (board por level id/version/players).

### Pendiente (polish)

1. Habilitar Workshop en App ID / legal agreement flow UX.
2. QA publish → subscribe en otra cuenta → jugar.
3. Tags / explorar Workshop (overlay o in-game) si Product lo pide.
4. Moderación: report Steam + validación JSON al cargar.

Paths reales (no `Content/Workshop/`):

```
Content/OfficialLevels/                         → oficiales shipped
%LocalAppData%/Color Blocks/UserLevels/         → Local
%LocalAppData%/Color Blocks/Workshop/{id}/      → suscritos
```

---

## Fase 5 — Release Steam

1. **App ID producción** en `steam_appid.txt` (hoy `4796400` en repo).
2. **SteamPipe**: depots Windows x64; quitar `steam_appid.txt` de build publicada.
3. **Steam Input Partner**: template Gamepad → Publish (ver [`STEAM_INPUT_OFFICIAL_SHIP.md`](STEAM_INPUT_OFFICIAL_SHIP.md)).
4. **Store page**: capturas, coop local/online, Workshop, leaderboards.
5. **Achievements** / **Cloud saves** (opcional v1).
6. **Beta branch** para online + Workshop antes de public.
7. **Legal**: EULA, privacidad si hay analytics.

---

## Orden sugerido

```
[Fase 0 rope QA] → [Fase 1 pulido] → [Fase 2 online QA]
        → [Fase 3 LB polish] ↔ [Fase 4 Workshop polish] → [Fase 5 release]
```

### Prioridad ahora

1. QA manual rope en niveles oficiales (`ColoredPhysics`).
2. Benchmark en cada cambio rope — `--benchmark rope`.
3. Fase 1 rápida (bindings, gitignore, audio).
4. Fase 2 QA — 2 clients, latencia 50–150 ms.
5. Partner: boards LB + Workshop + Steam Input Publish + rich presence tokens.

---

## Referencias en código

| Tema | Archivos clave |
|------|----------------|
| Rope / física | `Entities/Rope.cs`, `Managers/PhysicsWorld.cs`, `Gameplay/GameplayTuning.cs` |
| Dev / benchmarks | `Developer/GameplayBenchmark/`, `docs/09-HERRAMIENTAS-DEV.md` |
| Red / sesión | `Networking/`, `Steam/SteamGameNetworkService.cs` |
| Steam lobby / invites | `SteamLobbyService`, `SteamInviteManager`, `SteamPartyService` |
| Leaderboards | `SteamLeaderboardService`, `LeaderboardScene` |
| Workshop | `SteamWorkshopService`, `LevelLibrary` |
| Replay / Ghost | `Replay/`, `SteamReplayService`, `SteamGhostService`, doc 10 |
| Niveles | `LevelSystem/LevelLibrary.cs`, `EditorScene` |
| UI | `docs/07-UI-NAVEGACION.md` |
| Framework (juegos nuevos) | `docs/Framework/` |
