# 03 — Input

## Regla central

**Acciones, no teclas.** El gameplay pregunta `JumpPressed`, no `Keys.Space`.

## Capas

```
Hardware (KB / Mouse / XInput / Steam Input)
    → Backend
    → Action map (rebindable)
    → Context (Menu | Gameplay | TextEntry | Photo…)
    → Game / UI
```

## Contextos

- **Menu:** navegación, confirm, cancel; stick como UI nav.
- **Gameplay:** move/look/actions; no mover foco de menú.
- Cambiar contexto al pausar / abrir overlay.

## Rebinding

- Persistido en settings/UserData.
- Conflict detection (dos acciones, misma tecla).
- Reset a defaults.
- Gamepad y teclado por separado.

## Controllers

- Soporte pad desde prototipo ([20_GameStandards](../20_GameStandards/README.md)).
- Glyphs: genéricos al inicio; Steam glyphs si hay Steam Input.
- Hot-plug: detectar connect/disconnect sin crash.

## Steam Input ready

Aunque v1 use solo XInput:

- Pensá action sets (Menu / Game).
- No asumas índices de botón Xbox eternamente en UI copy.
- Partner Publish es obligatorio para ship pad “plug and play” — ver [10_Steam](../10_Steam/README.md).

## Pitfalls

- Mismo frame: UI y gameplay consumen Confirm → suppress tras cambio de escena.
- Steam Input “live” idle tapa XInput → demote/fallback.
- Hardcodear controles en tutoriales sin leer bindings.

Siguiente: [04_UI](../04_UI/README.md).
