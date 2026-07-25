# 05 — Steam Input

## Meta

**Abrir juego → pad anda** en cuenta limpia, sin tocar Controller settings.

## Capas

1. **Steam Input** (si cliente Steam + layout live) → acciones → tu backend.
2. **Fallback XInput / GamePad** si Steam Input idle o no init.
3. **Glyphs** opcionales desde Steam para prompts UI.

## Archivos típicos

- `steam_input_manifest.vdf` — action sets / actions.
- `controller_*.vdf` — layouts bundled (Official).
- Copiar al output junto al exe (`Steam/` o raíz).

## Código

- `SetInputActionManifestFilePath` **antes** de `SteamInput.Init`.
- `RunFrame` cada frame **antes** de leer input de juego.
- Solo reclamar slot si acciones reportan activas (evitar “fantasma” que tapa XInput).
- Dead-live demotion: si Steam “live” pero idle y hay pad XInput → caer a XInput.

## Partner (crítico)

Shipping VDF en depot **no alcanza**.

1. App Admin → Steam Input ON + familias de pads.
2. Template default = **Gamepad** (Valve) → **Save + Publish**.
3. Verificar en cuenta **sin Your Layouts**: Recommended no vacío.
4. Official Custom Bundled = paso **después**, solo si glyphs/actions custom estables.

| Tab Steam | Quién lo ve |
|-----------|-------------|
| Recommended → Gamepad | Jugadores nuevos (ship default) |
| Recommended → Official | Tras Custom Bundled publicado |
| Your Layouts | Solo quien editó (dev trap) |

## Checklist

- [ ] Manifest path set before Init
- [ ] RunFrame ordering correcto
- [ ] Fallback XInput funciona sin Steam
- [ ] Partner Template Gamepad Published
- [ ] Test cuenta limpia / amigo

**Referencia CB:** [`../05-STEAM.md`](../05-STEAM.md), [`../STEAM_INPUT_OFFICIAL_SHIP.md`](../STEAM_INPUT_OFFICIAL_SHIP.md), `SteamInputManager`.
