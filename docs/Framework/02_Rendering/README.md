# 02 — Rendering

Agnóstico a 2D/3D. El Framework no impone “un pixel blanco”; impone **separación**.

## Principios

1. **Sim no dibuja.** El tick produce estado; el renderer lee estado (o snapshot interpolado).
2. **Una estrategia de resolución** documentada (letterbox, scale-to-fit, integer scale).
3. **Cámaras** son presentación; no mueven lógica de colisión salvo diseño explícito.
4. **Batching:** en 2D, minimizar `Begin/End` de SpriteBatch; atlases cuando el volumen crece.
5. **UI** en espacio de pantalla o canvas lógico aparte del mundo.

## 2D

- SpriteBatch + sort modes conscientes (texture vs layer).
- Atlases / sprite sheets para reducir swaps.
- Texto: fuente propia o SpriteFont; medir strings para layout.

## 3D

- Separar update de transforms de pass de render.
- Frustum culling y LOD como presupuesto, no como afterthought.
- UI generalmente ortográfica post-3D.

## Resolución y display

Soportar desde día 1 (ver GameStandards):

- Windowed / Borderless / Fullscreen
- Lista de resoluciones razonables
- DPI awareness en Windows

## Photo / screenshot mode

Si existe: pausar o congelar sim opcional; no acoplar a un género. Ocultar HUD según flag.

## Anti-patrones

- Lógica de daño/colisión en `Draw`.
- Paths de texturas hardcodeados a máquina de un artista.
- UI que asume 1280×720 fijo sin layout.

Siguiente: [03_Input](../03_Input/README.md), [09_Performance](../09_Performance/README.md).
