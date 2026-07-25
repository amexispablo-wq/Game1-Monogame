# Prompt: Implement Localization

Prepará el proyecto para localización según GameStandards (localization-ready).

- Keys en vez de strings UI hardcodeados (al menos menús)
- Tabla/idioma default
- Fallback a key o inglés si falta traducción
- Font que soporte el charset target (documentá límite)
- No concatenes frases rotas; usá formato con placeholders

v1 puede shippear un solo idioma si la infra de keys existe.
