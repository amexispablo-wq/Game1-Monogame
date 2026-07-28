# 04 — Niveles y Editor

## Modelo de nivel

- **Runtime:** `LevelSystem/Level.cs` — geometría viva (platforms, goals, checkpoints, launch pads, power-ups), `PlayerStart`, `WorldSize` (auto-calculado con padding de 200px), `Name`, `MusicId` y flags.
- **Serializable:** `LevelSystem/LevelData.cs` — DTO con atributos `JsonPropertyName`. Conversión `Level.FromData(data)` / `level.ToData()`.
- `Level.CreateDefault()` genera un nivel de ejemplo (fallback si falla la carga).

### Flags del nivel (en `LevelData`)

`AllPlayers` (default true), `Player1..4`, `ColoredRope`, `RegularRope`, `LavaRise`. Nota: algunos flags persisten; verificar gameplay cableado antes de depender de ellos.

## Fuentes — `LevelSource`

| Source | Disco | Editable |
|--------|-------|----------|
| **Official** | `Content/OfficialLevels/` (runtime + source del repo vía `TryGetProjectOfficialLevelsRoot`) | Solo con `developerMode` |
| **Local** | `%LocalAppData%/Color Blocks/UserLevels/` | Sí |
| **Workshop** | `%LocalAppData%/Color Blocks/Workshop/{id}/level.json` | Read-only; editar = “Create Local Copy” |

Paths centralizados en `LevelSystem/LevelContentPaths.cs` + `UserDataPaths`.

> No hay `LevelManager`. API vigente: **`LevelLibrary`** (estático).

## Formato JSON

Mismo `LevelData` para Official / Local / Workshop. Serializado con `System.Text.Json` (`WriteIndented`, case-insensitive, enums como string).

```json
{
  "name": "Level 1",
  "platforms": [
    {
      "x": 0, "y": 400, "width": 800, "height": 40, "color": "Red",
      "moveVertical": false, "verticalSpeed": 20, "verticalDistanceBlocks": 2, "verticalDirection": "Up",
      "moveHorizontal": false, "horizontalSpeed": 15, "horizontalDistanceBlocks": 1, "horizontalDirection": "Right",
      "colorChangeEnabled": false, "colorCycle": ["Red", "Blue"], "colorChangePeriodSeconds": 2, "colorChangePhaseSeconds": 0
    }
  ],
  "goals": [
    { "x": 1216, "y": 356 }
  ],
  "checkpointFlags": [
    { "id": 1, "x": 600, "y": 320 }
  ],
  "launchPads": [
    { "x": 300, "y": 380, "width": 96, "height": 36, "rotation": 0 }
  ],
  "playerSpawn": { "x": 100, "y": 300 },
  "musicId": "default",
  "allPlayers": true,
  "player1": false, "player2": false, "player3": false, "player4": false,
  "coloredRope": false, "regularRope": false, "lavaRise": false
}
```

- `color`: `"Red" | "Blue" | "Green" | "White"`. Es el color de inicio (también `colorCycle[0]` si hay ciclo).
- Plataformas pueden tener motion ping-pong opcional en **un solo eje** (`moveVertical` XOR `moveHorizontal`). Speed en px/s (step 5), distance en blocks de 32px. Dirección: `verticalDirection` (`Up`/`Down`), `horizontalDirection` (`Left`/`Right`). Defaults: off / Up / Right.
- Color cycle opcional (independiente de motion): `colorChangeEnabled`, `colorCycle` (lista 2+), `colorChangePeriodSeconds` (segundos por paso, loop), `colorChangePhaseSeconds` (offset). Campos ausentes = off (niveles viejos OK). Editor: panel bajo motion; preview live con tiempo del editor.
- `rotation` de launch pad en grados (normalizada).
- `powerUps[]` opcional (backward compatible: ausente → vacío). Props: `type` (`Speed`/`Jump`), `durationSeconds`, `multiplier`, `respawnSeconds` (0 = sin respawn), `consumable` (default true).
- Launch pads: `launchForce` opcional (default 980).
- Plataformas/pads/power-ups con width o height <= 0 se ignoran al cargar.

## Gestión — `LevelSystem/LevelLibrary.cs`

| Método | Qué hace |
|--------|----------|
| `Initialize()` | UserDataPaths, folders, migration |
| `GetOfficialLevels` / `GetLocalLevels` / `GetWorkshopLevels` | Listas por fuente |
| `GetAllLevels()` | Official + Workshop + Local |
| `GetEditableLevels()` | Local + Workshop (+ Official si dev) |
| `GetLevel` / `LoadLevel` / `SaveLevel` | Por `levelId` tipado (`LevelIdentity`) |
| `CreateNewLevel` / `DeleteLevel` / `DuplicateLevel` | CRUD local (y official en dev) |
| `TryGetNextLevelId` | Siguiente en misma fuente (orden Level Select) |

- `LevelMetadata`: id tipado, nombre, path, source, versión, autor, etc.
- Workshop sync: `SteamWorkshopService` escribe bajo `Workshop/{id}/`; `LevelLibrary` solo lista.

> Legado: `Managers/LevelStorage.cs` / `Content/Levels/` / `Content/level.json` — no usar para features nuevas.

## Mejores tiempos — `Managers/BestTimeStorage.cs`

- Mejor tiempo por `levelId` (paths bajo UserData / por source).
- `SaveIfRecord`, `TryGetBestTime`, reset/delete, redondeo a centisegundos.
- Display `MM:SS:CS`. Leaderboards Steam usan el mismo criterio de score (ver `05-STEAM.md`).

## Música / previews

- `LevelMusicLibrary` — `MusicId` por nivel.
- `LevelPreviewManager` — preview PNG (`System.Drawing.Common`) para Level Select / info.

## Workshop (jugador)

1. Editor / Level Select → publicar nivel **Local** (`SteamWorkshopService`; Official rechazado).
2. Suscripciones Steam → sync a `%LocalAppData%/…/Workshop/{id}/level.json`.
3. Level Select lista Workshop read-only; editar → Duplicate a Local.

## Editor — `Scenes/EditorScene.cs`

- Grid snap 32, create/move/resize, multi-select, Goal/Checkpoint/LaunchPad/PowerUp toolbar.
- Color de plataforma, panel de motion (V/H ping-pong), panel color cycle (loop + period + swatches), copy/paste, pan/zoom, dirty flag.
- **Properties panel (derecha):** aparece solo si selección homogénea de Platform / LaunchPad / PowerUp / Lava. Mixed selection → sin panel.
- PowerUp: Type / Duration / Multiplier / Respawn / Consumable. LaunchPad: Launch Force / Rotation.
- Guarda con `LevelLibrary.SaveLevel`.

### Flujo

```
LevelSelectScene(EditMode)
  ├─ Create New → Popup → LevelLibrary.CreateNewLevel → EditorScene
  ├─ Edit       → EditorScene(levelId)
  ├─ Duplicate  → Local copy (Workshop/Official)
  └─ Delete     → Popup → LevelLibrary.DeleteLevel
```

## UI de soporte

Widgets en `UI/`: `Button`, `Slider`, `Checkbox`, `Dropdown`, `Popup`, layouts responsive. Ver [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md).
