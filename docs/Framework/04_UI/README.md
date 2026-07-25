# 04 — UI

## Baseline de todo juego

Por [20_GameStandards](../20_GameStandards/README.md):

- Main Menu
- Pause Menu
- Options (gráficos, audio, input)
- Build version visible
- Navegable con teclado, mouse y pad

## Focus graph

- Controles focusables registrados explícitamente.
- Links up/down/left/right (grafo), no solo “el más cercano”.
- Un `FocusManager` por escena (o modal).
- Debug de foco en developer mode.

## Modales

- Popups de confirmación / invites / errores: dueño claro (escena o host).
- Mientras el modal está abierto: **bloquear** update de la escena detrás o consumir input.
- Fade-in corto OK; no bloquear el thread.

## Layout

- Recalcular por viewport o usar anchors.
- Probar 720p y ultrawide temprano.
- No cards innecesarias: claridad > decoración (producto decide estética).

## Options

| Bloque | Contenido mínimo |
|--------|------------------|
| Display | Resolución, modo ventana |
| Audio | Master / Music / SFX |
| Input | Rebind KB + pad |
| Accesibilidad | (evaluar) subtítulos, paletas, shake |

Persistir al aplicar o al salir — documentar cuál.

## Pitfalls

- Click-through al cambiar escena (A/Enter residual).
- Foco perdido al refrescar lista dinámica (rebuild + restore index).
- Texto no localizado hardcodeado (preparar keys aunque v1 sea un idioma).

Siguiente: [05_SaveSystem](../05_SaveSystem/README.md).
