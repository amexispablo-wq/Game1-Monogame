# 06 — Guía de Desarrollo

## Requisitos

- **.NET 9 SDK** (target `net9.0`, plataforma `x64`).
- **MonoGame 3.8** (vía NuGet, no requiere instalación global salvo para usar `mgcb-editor`).
- Windows x64 (DLL nativa de Steam es `steam_api64.dll`). El proyecto es DesktopGL (multiplataforma en teoría), pero el binario nativo de Steam incluido es Windows-x64.
- (Opcional) Cliente de Steam corriendo; si no, el juego corre con Steam deshabilitado.

## Build y ejecución

```bash
# Restaurar + compilar
dotnet build "Color Blocks.csproj" -c Debug

# Ejecutar
dotnet run --project "Color Blocks.csproj"

# Release
dotnet build "Color Blocks.csproj" -c Release
```

- El output va a `bin/Debug/net9.0/` (o `Release`). Ahí se copian `Content/`, `Levels/`, `steam_appid.txt` y `steam_api64.dll`.
- Los niveles en runtime se leen/escriben desde `AppContext.BaseDirectory/Content/Levels` (el output), **no** desde el source tree. Para editar niveles "de fábrica", trabajá en el build output o copialos de vuelta.

## Settings — `Managers/SettingsManager.cs` + `Utils/GameSettings.cs`

- `SettingsManager.Initialize()` se llama en el constructor de `ColorBlocksGame`.
- `CurrentSettings` (`GameSettings`): resolución, display mode, FPS limit, music volume, **keybindings**, **gamepad bindings**.
- Keybindings teclado (default): `MoveLeft=A`, `MoveRight=D`, `Jump=W`, `Respawn=R`, `FastFall=S`, `PullRope=Space`, `Red=J`, `Blue=K`, `Green=L`.
- Gamepad: defaults en `Managers/GamepadDefaults.cs`; rebinding en Options para Jump/Respawn/Red/Blue/Green (Move/FastFall/PullRope son axis/trigger fijos).
- Cambios de gráficos: `ColorBlocksGame.ApplyGraphicsSettings` + `ApplyFrameSettings`.
- `OptionsScene`: edita pending settings; Apply guarda y recarga input.

> **Deuda:** verificar que `SettingsManager.SaveSettings` persista `GamepadBindings` (hoy copia `Keybindings` pero gamepad puede no sobrevivir restart).

## Navegación UI

Ver [`07-UI-NAVEGACION.md`](07-UI-NAVEGACION.md).

- Todas las escenas de menú usan `UIFocusManager` con grafo explícito.
- Agregar widget: `Focusable*` wrapper + `Add(id)` + `Link` vecinos + `FinalizeFocus(defaultId)`.

## Debugging

| Tecla | Contexto | Qué muestra |
|-------|----------|-------------|
| **F3** | `GameScene` | HUD gameplay: tick, snapshots, input buffer, sesión, Steam, rol/autoridad, eyección |
| **F8** | Global (InputManager) | Toggle overlay navegación UI: IDs, vecinos, líneas, panel |
| **F9** | Global (InputManager) | Step foco al siguiente widget; log consola + cadena magenta en overlay |

- `Player.Draw(debugDraw)` — vectores velocidad/aceleración, eyección.
- `Rope.Draw(debugDraw)` — nodos, tensión, pull.
- `Esc` — pantalla anterior; en menú principal, sale del juego.

## Convenciones de código

- **Namespace único:** `ColorBlocks` (file-scoped namespaces).
- `#nullable enable` en archivos nuevos.
- **No usar comentarios narrativos obvios.** Comentar sólo intención/no-obvio.
- Render: usar `game.Pixel` (1x1) + `DrawHelper` + `SimpleTextRenderer`. No hay pipeline de sprites.
- **Gameplay determinista** → dentro de `GameSimulation.StepFixedTick` (tick fijo). Cámara/efectos visuales → por frame en la escena.
- Entidades en red: implementar `INetworkEntity` + `CreateSnapshot()`/`ApplySnapshot()`; configurar `NetworkEntityOwnership`.
- Estructuras de datos de red preferir `readonly record struct` (ver `PlayerInputState`, `TickRate`, `NetworkEntityOwnership`).
- Inputs nuevos: agregar a `PlayerInputState` (y `Sanitized()`) para que entren al buffer y la red.

## Añadir una mecánica nueva (receta)

1. Si es input: agregar campo a `PlayerInputState` + leerlo en `InputManager`.
2. Lógica determinista: agregar a `Player`/`PhysicsWorld` y llamarla desde `GameSimulation.StepFixedTick`.
3. Si tiene estado replicable: agregarlo a snapshots y `CreateSnapshot`/`ApplySnapshot`.
4. Si es objeto de nivel: entidad en `Entities/`, DTO en `LevelData`, soporte en `EditorScene`.

## Añadir una escena nueva

1. Implementar `IScene` (`Update`, `Draw`, `OnExit`).
2. Layout responsive en cada frame.
3. `UIFocusManager` + grafo + `FinalizeFocus` — ver doc 07.
4. Navegar con `game.ChangeScene(new MiScene(game, ...))`.

## Archivos para ignorar / generados

- `obj/`, `bin/` (build). Idealmente agregar al `.gitignore`.
- `.preview_png_debug/` — proyecto auxiliar para debug de previews PNG; no es parte del juego.

## Riesgos / deuda técnica conocida

- **Coop online sin transporte de gameplay** — lobby OK, simulación no sincronizada (doc 03, roadmap 08).
- **Gamepad binding persistence** — posible bug en `SettingsManager`.
- `LevelManager.RenameLevel` es placeholder.
- Flags `LavaRise` / `PlayerN` persisten; gameplay puede estar incompleto.
- Niveles en build output → fácil perder al limpiar `bin/`.
- `bin`/`obj` parcialmente en git.
- Leaderboards globales y Workshop: no iniciados — ver [`08-ROADMAP.md`](08-ROADMAP.md).
