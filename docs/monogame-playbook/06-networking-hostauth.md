# 06 — Networking host-authoritative

## Recomendación v1

**Host simula todo.** Clientes envían input; reciben snapshots; aplican estado.

No empezar con predicción/rollback. Primero lobby estable + transporte + 2 clients jugables.

## Piezas

| Pieza | Rol |
|-------|-----|
| Transporte | Steam Networking Messages/Sockets o equivalente |
| `InputFrame` | Input por tick / player id |
| `GameSnapshot` | Estado replicable (posiciones, timer, ropes…) |
| Buffer input | Host mergea remoto + local por tick |
| `GameSession` | Rol Host/Client, peers, settings (tick rate) |

## Loop

```
Host:   PumpIncoming → FixedTick(sim) → BroadcastSnapshot
Client: SendLocalInput → ReceiveSnapshot → ApplySnapshot
```

## Ownership

- Cada entidad: `NetworkId`, `OwnerId`, `IsHostControlled`.
- Host: simula todo.
- Cliente v1: **no** simular remoto; solo aplicar snapshot (predicción = fase 2).

## Qué posponer

- Client-side prediction + reconciliation
- Interpolación/extrapolación smooth
- Host migration
- Mid-game join spawn complejo

## Checklist v1

- [ ] 2 peers mismo lobby → misma sesión
- [ ] Input remoto mueve personaje en host
- [ ] Cliente ve estado coherente (aunque steppy)
- [ ] Leave/disconnect vuelve a lobby/menú
- [ ] Debug: rol + seq snapshot en overlay

**Referencia CB:** [`../03-NETWORKING-COOP.md`](../03-NETWORKING-COOP.md), `SteamGameNetworkService`, `GameNetworkCoordinator`, `GameSimulation`.
