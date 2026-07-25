# Prompt: Implement Save System

Implementá un save system para este proyecto MonoGame siguiendo `docs/Framework/05_SaveSystem` y `20_GameStandards`.

Requisitos:
- Root en UserData / LocalAppData (no Content/)
- Campo `version` en el formato serializado
- Escritura atómica (temp → rename)
- API clara: Save / Load / Exists / Delete
- Logs en fallo; no crash
- Settings separados del save de progreso si aplica
- Tests mentales: “save viejo” → migración stub o rechazo con mensaje

No asumas género. Integrá con el host `Game` existente y documentá paths.
