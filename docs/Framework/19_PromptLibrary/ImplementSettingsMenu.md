# Prompt: Implement Settings Menu

Implementá Options/Settings (escena o overlay) según `docs/Framework/04_UI` y `20_GameStandards`.

Incluí:
- Display: resolución, Windowed/Borderless/Fullscreen
- Audio: music / sfx (y master si cabe)
- Persistencia vía Settings store en UserData versionado
- Navegación teclado + mouse + gamepad (focus graph)
- Apply / Back sin romper escena anterior (suppress confirm)

No hardcodees resoluciones mágicas; usá catálogo o modos del monitor.
