# 04 — Niveles y Editor

## Modelo de nivel

- **Runtime:** `LevelSystem/Level.cs` — geometría viva (platforms, goals, checkpoints, launch pads), `PlayerStart`, `WorldSize` (auto-calculado con padding de 200px), `Name`, `MusicId` y flags.
- **Serializable:** `LevelSystem/LevelData.cs` — DTO con atributos `JsonPropertyName`. Conversión `Level.FromData(data)` / `level.ToData()`.
- `Level.CreateDefault()` genera un nivel de ejemplo (usado como fallback si falla la carga).

### Flags del nivel (en `LevelData`)

`AllPlayers` (default true), `Player1..4`, `ColoredRope`, `RegularRope`, `LavaRise`. Nota: `LavaRise` y los flags por jugador están **definidos y persistidos** pero su gameplay puede no estar completamente cableado — verificar antes de depender de ellos.

## Formato JSON

Archivos en `Content/Levels/level_N.json`. Serializado con `System.Text.Json` (`WriteIndented`, case-insensitive, enums como string).

```json
{
  "name": "Level 1",
  "platforms": [
    { "x": 0, "y": 400, "width": 800, "height": 40, "color": "Red" }
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

- `color`: `"Red" | "Blue" | "Green"`.
- `rotation` de launch pad en grados (normalizada).
- Plataformas/pads con width o height <= 0 se ignoran al cargar.

## Gestión de niveles — `LevelSystem/LevelManager.cs` (estático)

| Método | Qué hace |
|--------|----------|
| `GetAllLevels()` | escanea `Content/Levels/level_*.json`, ordena por número, devuelve `LevelMetadata` |
| `GetLevel(id)` | metadata de un nivel |
| `LoadLevel(id)` | carga `Level` (fallback a `CreateDefault` si falla) |
| `SaveLevel(level, id)` | serializa a `level_id.json` |
| `CreateNewLevel(name)` | crea `level_N.json` vacío con el siguiente N libre |
| `DeleteLevel(id)` | borra archivo + su récord en `BestTimeStorage` |
| `RenameLevel(...)` | **placeholder**, sin implementar |

- Directorio: `AppContext.BaseDirectory/Content/Levels` (es decir, junto al ejecutable en el build output, no en el source tree).
- `LevelMetadata`: `Id` (ej. `level_1`), `Name` (display, ej. `Level 1` o el `name` del JSON), `FilePath`.

> Nota: hay también `Managers/LevelStorage.cs` (carga/crea un `level.json` legado de un solo nivel) y `Content/level.json`. El sistema vigente es el multi-nivel de `LevelManager`.

## Mejores tiempos — `Managers/BestTimeStorage.cs`

- Guarda mejor tiempo por `levelId` (en `best_times.json`).
- `SaveIfRecord(id, time)` devuelve true si es récord nuevo.
- `TryGetBestTime`, `ResetLevelRecord`, `DeleteLevelRecord`, `RoundToCentiseconds`.
- Tiempos en segundos (float), redondeados a centisegundos. Formato display `MM:SS:CS`.

## Música — `LevelSystem/LevelMusicLibrary.cs`

- `MusicId` por nivel (default `LevelMusicLibrary.DefaultMusicId`). Biblioteca de pistas seleccionables en el editor / info de nivel.

## Previews — `LevelSystem/LevelPreviewManager.cs`

- Genera una textura de preview del nivel (usa `System.Drawing.Common`) para mostrar en `LevelSelectScene` y `LevelInfoScene`.

## Editor de niveles — `Scenes/EditorScene.cs`

Editor completo (~1800 líneas). Funcionalidad:

- **Grid snapping** (`GridSize = 32`, toggle).
- **Crear/seleccionar/mover/redimensionar** plataformas (con handles de resize, margen 8px, tamaño mínimo 1 celda).
- **Selección múltiple** y arrastre grupal (plataformas, checkpoints, launch pads).
- **Goal, Checkpoints, Launch Pads:** arrastrables desde una toolbar al mundo.
- **Color** de plataforma seleccionable (`GameColor`).
- **Copiar/Pegar** (`EditorClipboardItem`, `_clipboard`, `_pasteCount`).
- **Paneo** de cámara y zoom (`Camera`).
- **Estado sucio** (`_isDirty`) para avisar cambios sin guardar.
- Guarda con `LevelManager.SaveLevel(_level, _levelId)`.

`EditorObjectKind`: enum de tipos de objeto que el editor maneja (None/Goal/Checkpoint/LaunchPad/...).

### Flujo de edición

```
LevelSelectScene(EditMode)
  ├─ Create New → Popup nombre → LevelManager.CreateNewLevel → EditorScene
  ├─ Edit       → EditorScene(levelId)
  └─ Delete     → Popup confirmación → LevelManager.DeleteLevel
```

## Sistema de UI (soporte del editor y menús) — `UI/`

Widgets reutilizables, todos dibujados con el pixel 1x1 + `SimpleTextRenderer`:

- `Button`, `Slider`, `Checkbox`, `Dropdown`, `CycleSelector<T>`, `ResolutionDropdown`, `DisplayModeSelector`, `TextInputComponent`, `Popup` (modal con fade).
- Layouts: `ButtonRowLayout`, `ButtonColumnLayout`, `GridLayout` (responsive). El layout se recalcula cada frame según el viewport.
