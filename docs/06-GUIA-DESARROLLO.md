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
- `CurrentSettings` (`GameSettings`): resolución (`ResolutionWidth/Height`), display mode, **keybindings** (`Dictionary<string,string>`), etc.
- Keybindings por acción (default): `MoveLeft=A`, `MoveRight=D`, `Jump=W`, `Respawn=R`, `FastFall=S`, `PullRope=Space`, `Red=J`, `Blue=K`, `Green=L`.
- Cambios de gráficos: `ColorBlocksGame.ApplyGraphicsSettings(w, h, displayMode)` (`fullscreen`/`windowed`/`borderless`).
- `OptionsScene` edita estos settings; `InputManager.ReloadProfilesFromSettings()` recarga bindings.

## Convenciones de código

- **Namespace único:** `ColorBlocks` (file-scoped namespaces).
- `#nullable enable` en archivos nuevos.
- **No usar comentarios narrativos obvios.** Comentar sólo intención/no-obvio.
- Render: usar `game.Pixel` (1x1) + `DrawHelper` + `SimpleTextRenderer`. No hay pipeline de sprites.
- **Gameplay determinista** → dentro de `GameSimulation.StepFixedTick` (tick fijo). Cámara/efectos visuales → por frame en la escena.
- Entidades en red: implementar `INetworkEntity` + `CreateSnapshot()`/`ApplySnapshot()`; configurar `NetworkEntityOwnership`.
- Estructuras de datos de red preferir `readonly record struct` (ver `PlayerInputState`, `TickRate`, `NetworkEntityOwnership`).
- Inputs nuevos: agregar a `PlayerInputState` (y `Sanitized()`) para que entren al buffer y la red.

## Debugging

- **`F3`** en `GameScene`: HUD de debug (tick, snapshots, input buffer, sesión, Steam, rol/autoridad por entidad, eyección, launch force).
- `Player.Draw(debugDraw)` dibuja vectores de velocidad/aceleración/normales y estado de eyección.
- `Rope.Draw(debugDraw)` dibuja nodos, tensión, pull y colisiones.
- `Esc` vuelve a la pantalla anterior; en menú, sale del juego.

## Añadir una mecánica nueva (receta)

1. Si es input: agregar campo a `PlayerInputState` + leerlo en `InputManager.ReadKeyboardInputState`.
2. Lógica determinista: agregar a `Player`/`PhysicsWorld` y llamarla desde `PhysicsWorld.UpdatePhysics` o `GameSimulation.StepFixedTick`.
3. Si tiene estado replicable: agregarlo a `PlayerSnapshot`/`RopeSnapshot`/`GameSnapshot` y a `CreateSnapshot`/`ApplySnapshot`.
4. Si es un objeto de nivel: crear entidad en `Entities/`, DTO en `LevelData`, conversión en `Level.FromData/ToData`, soporte en `EditorScene` y dibujo en `Level.Draw`.

## Añadir una escena nueva

1. Implementar `IScene` (`Update`, `Draw`, `OnExit`).
2. Layout responsive en cada frame (mirar `MenuScene.LayoutButtons` / `ButtonColumnLayout`).
3. Navegar con `game.ChangeScene(new MiScene(game, ...))`.

## Archivos para ignorar / generados

- `obj/`, `bin/` (build). Nota: hay archivos de `obj/` trackeados en git (`obj/x64/Debug/.../Color Blocks.AssemblyInfo*`) — idealmente agregar `bin/` y `obj/` al `.gitignore`.
- `.preview_png_debug/` es un proyecto auxiliar (`net10.0`) para debug de generación de previews PNG; no es parte del juego.

## Riesgos / deuda técnica conocida

- **Coop online sin transporte** (la pieza grande pendiente — doc 03).
- `LevelManager.RenameLevel` es un placeholder.
- Flags de nivel `LavaRise` / `PlayerN` persisten pero su gameplay puede estar incompleto.
- Niveles viven en el build output → fácil perderlos al limpiar `bin/`. Considerar copiarlos como contenido versionado.
- `bin`/`obj` parcialmente en git.
