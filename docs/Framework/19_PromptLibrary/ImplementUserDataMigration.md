# Prompt: Implement UserData Migration

Pipeline de migración según `docs/Framework/05_SaveSystem`.

- Detectar version en disco
- Cadena vN → vN+1
- Backup `.bak` antes de escribir
- Log cada paso
- Si falla: dejar backup + mensaje
- Cubrir settings y saves (separado si hace falta)

Escribí test manual: crear archivo v1, abrir build nueva, verificar vCurrent.
