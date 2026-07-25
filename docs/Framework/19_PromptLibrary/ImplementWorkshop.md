# Prompt: Implement Workshop

Implementá Steam Workshop para **user content** según `docs/Framework/10_Steam`.

- Publish solo desde contenido user (no official shipped)
- Sync subscriptions a UserData/Workshop/{id}/
- Validación + version en formato
- UI: lista / subscribe state / errores legal agreement
- LevelLibrary-equivalente: no mezclar con Content oficial
- Edit path: copy-on-write a local

Gameplay no llama SteamUGC directo.
