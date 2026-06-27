# 07 — UI y Navegación

Sistema de foco unificado para teclado, gamepad y mouse en todos los menús del juego. Reemplaza navegación geométrica automática por un **grafo explícito** de vecinos (Up/Down/Left/Right).

## Carpeta `UI/Navigation/`

| Archivo | Clase | Rol |
|---------|-------|-----|
| `UIFocusManager.cs` | `UIFocusManager` | Registro de widgets por ID, grafo, input, foco sticky, validación, overlay debug |
| `NavigationGraph.cs` | `NavigationGraph` | Grafo dirigido; helpers `WireGrid`, `WireVerticalChain`, `LinkPair` |
| `NavigationGraphBuilder.cs` | `NavigationGraphBuilder` | `LinkGridBottomRowTo` — conecta última fila de grilla con control debajo |
| `NavigationDirection.cs` | `NavigationDirection` | Enum: Up, Down, Left, Right |
| `IFocusable.cs` | `IFocusable` | Contrato: bounds, confirm/cancel, edit mode, direcciones |
| `Focusables.cs` | Varios `Focusable*` | Wrappers: Button, CycleSelector, Slider, Checkbox, Dropdown, ResolutionDropdown, TextInput, Action, GridCell, CycleMemberInput |
| `EditModeController.cs` | `EditModeController<T>` | Modo edición en dos pasos (confirmar entra, cancel restaura snapshot) |
| `InputNavigationService.cs` | `InputNavigationService` | Dispositivo activo: Mouse / Keyboard / Gamepad |
| `NavigationDebug.cs` | `NavigationDebug` | F8 overlay, F9 step-through, validación en consola |
| `FocusHighlight.cs` | `FocusHighlight` | Anillo animado de foco |
| `VirtualCursor.cs` | `VirtualCursor` | Cursor con stick derecho en editor (gamepad sin mouse) |
| `ResolutionCatalog.cs` | `ResolutionCatalog` | Lista de resoluciones filtrada por monitor |

## UIFocusManager — comportamiento

- Widgets registrados con **IDs estables** (`Add(item, "Play")`).
- Navegación direccional vía `NavigationGraph` explícito (no auto-detect espacial).
- **Mouse:** foco solo en **click** (no hover) — evita flicker gamepad/mouse.
- **Gamepad:** stick/D-pad con repeat delay 0.2s.
- **Teclado:** flechas + Tab/Shift+Tab (vertical), Enter confirmar, Escape cancelar.
- `FinalizeFocus(defaultId)` restaura foco sticky tras rebuild del grafo.
- `FocusById(id)` / `SetDefaultFocus(id)` para foco programático.
- `ValidateNavigation()` chequea IDs duplicados, dead ends, nodos inalcanzables, editables sin cancel.

### Foco sticky

Si el grafo se reconstruye (layout responsive cada frame), el manager intenta mantener el widget con el mismo ID. Si no existe, cae al `defaultId` de `FinalizeFocus`.

## Modo edición (EditModeController)

Para sliders, cycle selectors y dropdowns:

1. Foco en widget → Enter/A entra en modo edición.
2. Flechas cambian valor.
3. Escape/B sale y restaura valor previo.
4. Enter/A confirma y sale.

Usado en `OptionsScene` (display mode, FPS, resolución, volumen) y selectores similares.

## Debug de navegación

| Tecla | Acción |
|-------|--------|
| **F8** | Toggle overlay: IDs de widgets, vecinos, líneas de conexión, panel de estado |
| **F9** | Avanza foco al siguiente widget en orden de traversal; log en consola |

El overlay **no cambia** el aspecto visual cuando F8 está apagado.

`NavigationDebug.CurrentScene` se actualiza en `ColorBlocksGame.ChangeScene`.

### Validación automática

Al entrar a una escena con F8 activo, `UIFocusManager` corre `ValidateNavigation()` una vez y loguea problemas (IDs duplicados, vecinos faltantes, etc.).

## Escenas y grafos

Cada escena reconstruye su grafo cada frame (o al abrir) y llama `FinalizeFocus`.

| Escena | Archivo | Patrón de grafo | Foco default |
|--------|---------|-----------------|--------------|
| Menu | `MenuScene.cs` | Cadena vertical: Play → Party → LevelEditor → Options | `"Play"` |
| Party | `PartyScene.cs` | Filas miembro (input/kick) → Play ↔ Invite ↔ Back | `"Play"` |
| Options | `OptionsScene.cs` | Display → Res → FPS → Audio → tabla controls (KB↔PAD por fila) → Apply/Back | `"DisplayMode"` |
| Level Select | `LevelSelectScene.cs` | Grilla niveles (`WireGrid`) → rope/lava/play (play) o acciones (edit) | `"Level0"` o `"Play"` |
| Level Info | `LevelInfoScene.cs` | Form vertical + grilla 2×2 jugadores + checkboxes; prompt separado para cambios sin guardar | `"LevelName"` |
| Editor | `EditorScene.cs` | Back ↔ Apply (+ lava speed ±); `VirtualCursor` para gamepad | `"Back"` |
| Game (muerte) | `GameScene.cs` | Opciones menú muerte | `"RespawnStart"` |
| Pausa | `PauseMenuOverlay.cs` | Cadena vertical opciones | Resume |
| Popups | `Popup.cs`, `AlertPopup.cs` | Confirm/Cancel | — |

### Options — Control Settings (layout)

Tabla de 3 columnas: **ACTION** (45%) | **KEYBOARD** (27%) | **GAMEPAD** (28%).

- 9 acciones rebindables en teclado; gamepad solo Jump/Respawn/Red/Blue/Green (Move/FastFall/PullRope son axis/trigger fijos).
- Cada celda KB y PAD es un `FocusableAction` con foco independiente.
- Navegación horizontal KB ↔ PAD por fila; vertical entre filas.
- Layout **content-driven**: panel crece con la tabla; sección Control Settings altura exacta; Back/Apply debajo sin overlap.
- `keyRowHeight` se calcula del espacio disponible (sin min que fuerce overflow).

## Input y dispositivos

`InputNavigationService` rastrea dispositivo activo y controla visibilidad del highlight de foco.

`InputManager` (F3 gameplay HUD, F8 nav debug, F9 step) — mouse activo solo en click/wheel, no en movimiento.

## Convenciones al agregar UI navegable

1. Crear `Focusable*` wrapper del widget.
2. `_focus.Add(focusable, "IdUnico")`.
3. Conectar vecinos con `_focus.Link(...)` o helpers del grafo.
4. Al final del rebuild: `_focus.FinalizeFocus("IdDefault")`.
5. Probar con F8 + F9; correr `ValidateNavigation` si hay problemas.

## Legado

`UI/MenuListNavigator.cs` — navegador vertical antiguo; reemplazado por `UIFocusManager` en la mayoría de escenas.
