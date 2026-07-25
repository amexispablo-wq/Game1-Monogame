# Prompt: Implement Steam Overlay

Integrá conciencia del Steam Overlay:

- Detectar overlay activo si la API lo permite
- Pausar o bloquear input de gameplay cuando el overlay captura foco (según diseño)
- No pelear con Shift+Tab
- Fail-soft si Steam off
- Log estado para debug F3

No implementes features de store; solo convivencia overlay ↔ juego.
