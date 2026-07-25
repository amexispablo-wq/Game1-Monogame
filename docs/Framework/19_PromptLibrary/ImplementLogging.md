# Prompt: Implement Logging

Sistema de logging según GameStandards / DebugTools.

- Niveles Info/Warn/Error
- Sink archivo bajo UserData/Logs (rotación simple)
- Sink consola en Debug
- Incluir build version en header de sesión
- Rate-limit en hot paths
- API estática o servicio inyectado — una sola

No loguees secretos ni tokens.
