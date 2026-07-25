# 07 — UI focus / navegación

## Problema

Mouse + teclado + gamepad en menús sin “foco perdido” ni A/Enter comiendo dos botones.

## Enfoque: grafo explícito

- Cada control focusable se registra en un `UIFocusManager`.
- Links vertical/horizontal entre índices (no “magia” por posición sola).
- Un solo `Update` de foco por escena (o modal global).

## Reglas

1. **Modales** (popup invite, confirm) actualizan **antes** y bloquean update de escena.
2. Tras cambiar escena, **suppress confirm** hasta release (evita click-through).
3. Rebinding: modo edición dedicado; no mezclar con navegación normal.
4. Debug: overlay de foco (quién tiene focus, links) — tecla dev.

## Gamepad

- Navigate = dpad/stick menú; Submit / Cancel acciones claras.
- Analog stick: contexto Menu vs Gameplay (no mover cursor menú in-game).

## Checklist

- [ ] Todo botón alcanzable con pad
- [ ] Mouse click + pad no pelean foco
- [ ] Popup no deja escena comer Submit
- [ ] Options rebind persiste (KB y pad)
- [ ] QA 720p y ultrawide sin overflow crítico

**Referencia CB:** [`../07-UI-NAVEGACION.md`](../07-UI-NAVEGACION.md), `UI/Navigation/`.
