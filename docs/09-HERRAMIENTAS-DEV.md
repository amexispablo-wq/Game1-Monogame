# 09 — Herramientas de desarrollo

Solo activas con **`developerMode: true`** en `developer_settings.json` (copiado al output junto al exe). Sin ese flag, F6/F8/F9/F10/F11 de dev y el botón **Rope Sandbox** no aparecen.

---

## Developer mode

| Archivo | Ubicación |
|---------|-----------|
| `developer_settings.json` | `AppContext.BaseDirectory` (junto al exe) o source tree al desarrollar |

```json
{ "developerMode": true }
```

`DeveloperSettings.Reload()` se llama al entrar a Level Select para recargar el flag.

---

## Rope Sandbox — `Scenes/RopeSandboxScene.cs`

Escena aislada para probar física de soga sin meta ni timer.

- **Entrada:** Menu → **Rope Sandbox** (solo dev mode).
- **Nivel:** `Level.CreateRopeSandbox()` — plataformas rojas, sin goal, `RegularRope = true`.
- **Modo soga:** `RopeGameplayMode.Neutral` (ignora colisión con plataformas).
- **Party:** `PartyManager.EnsureDevSandboxMembers()` garantiza ≥2 jugadores.
- **Input:** bindings vía `SpawnFromParty`; al salir de `GameScene` se resetea `GameplayInputBlocked` en `ClearGameplayBindings()`.

Teclas in-game: **ESC** menú, **F3** debug, **F6** tuning panel.

> **Pendiente opcional:** selector de `ColoredPhysics` en sandbox para probar color-collision sin jugar un nivel completo.

---

## Tuning en vivo — `Gameplay/DeveloperTuningPanel.cs`

Panel F6 durante `GameScene` y `RopeSandboxScene`. Edita `GameplayTuning.Active` y reaplica a jugadores/cuerdas cada frame.

Parámetros expuestos: masa/fricción/aceleración/salto del jugador; rest length, slack, stiffness, damping, iteraciones, pull, fuerzas máximas y node count de la soga.

`GameplayTuning.ApplyTo(Rope)` usa mínimo **40 nodos** en `ColoredPhysics` (`Rope.ColoredPhysicsMinNodeCount`).

---

## Gameplay Benchmark — `Developer/GameplayBenchmark/`

Harness headless + overlay in-game para regresiones de física y rope.

### In-game

| Tecla | Acción |
|-------|--------|
| **F10** | Toggle overlay / correr suite |
| **F11** | Toggle debug del runner |
| **Ctrl+F10** | Forzar export report (dev) |

Overlay: `BenchmarkOverlay`, stats en `BenchmarkDebugOverlay`. Manager global: `BenchmarkManager` (update en `ColorBlocksGame`).

### CLI headless

```bash
dotnet run --project "Color Blocks.csproj" -- --benchmark all
dotnet run --project "Color Blocks.csproj" -- --benchmark rope
dotnet run --project "Color Blocks.csproj" -- --benchmark movement
dotnet run --project "Color Blocks.csproj" -- --benchmark replay
dotnet run --project "Color Blocks.csproj" -- --benchmark fuzz
```

Requiere `developerMode: true`. Report JSON: `Developer/GameplayBenchmark/GameplayBenchmarkReport.json`.

### Categorías y escenarios

| Categoría | Escenarios clave |
|-----------|------------------|
| **Rope** | `rope.regular.suite`, `rope.colored.suite` — 14 mecánicas cada uno |
| **Movement** | aceleración, salto, eyección |
| **Replay** | determinismo de reproducción |
| **Performance** | costo por tick |
| **Fuzz** | inputs aleatorios + replay de fallos en `Developer/FuzzFailures/` |

### Suite de mecánicas rope (`RopeMechanicsSimulation`)

Cada suite (neutral + colored) corre:

1. Slack  
2. Maximum stretch  
3. Compression / sag  
4. Pull  
5. Swing  
6. Vertical hang  
7. Horizontal co-move  
8. Repeated jumping  
9. Repeated pulling  
10. Recovery  
11. Colored platform collision (colored)  
12. Diagonal platform blockage (colored)  
13. Pull stability  
14. Segment penetration guard  

Validadores auxiliares: `RopeCollisionValidator`, `BenchmarkPhysicsValidator`, `BenchmarkSnapshotComparer`.

### Agregar un benchmark

1. Crear clase que extienda `BenchmarkScenario` en `Developer/GameplayBenchmark/Scenarios/`.
2. Registrar en `BenchmarkRegistry` (auto-discovery por reflection al iniciar).
3. Usar `BenchmarkHarness` + `ScriptedInputSource` para simular ticks deterministas.
4. Devolver `BenchmarkResult` con `BenchmarkAssertion` (Pass/Warn/Fail).

---

## Replay debug (dev)

| Tecla | Acción |
|-------|--------|
| **F10** | Force-save replay (sin dev: abre viewer shortcut según contexto) |
| **F11** | Toggle background replay |

Ver `Replay/ReplayRecorder.cs`, `ReplayDiagnostics`.

---

## Checklist antes de tocar rope

1. Correr `--benchmark rope` (headless).
2. Probar sandbox manual (2 jugadores, pull, plataformas).
3. Probar `ColoredPhysics` en nivel real con mezcla de colores (R+G → amarillo en soga).
4. Si cambiás tuning default, actualizar `GameplayTuning` **y** asserts del benchmark si aplica.
