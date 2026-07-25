# 01 — Bootstrap MonoGame

## Stack recomendado

- .NET 8/9, `WinExe` o `Exe`, `x64`.
- `MonoGame.Framework.DesktopGL` + Content Builder Task.
- Steam opcional: `Steamworks.NET` + `steam_api64.dll` copiada al output.
- `#nullable enable` en código nuevo.

## Esqueleto mínimo

```
Game/
  Program.cs          → new Game().Run()
  MyGame.cs           → : Game
Scenes/
  IScene.cs
  MenuScene.cs
Core/ or Simulation/  → tick fijo si hay gameplay determinista
Content/
  Content.mgcb
```

## Loop MonoGame

1. `Initialize` — servicios (Steam, input).
2. `LoadContent` — `SpriteBatch`, assets, primera escena.
3. `Update` — callbacks Steam → input → escena (o modal global).
4. `Draw` — clear → escena → HUD overlays.
5. `ChangeScene` — `OnExit` saliente → asignar nueva.

## Patrón IScene

```csharp
public interface IScene
{
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime, SpriteBatch spriteBatch);
    void OnExit();
}
```

Layout responsive por frame OK al inicio; cachear solo si hace falta.

## Tick fijo (si hay sim/red)

- Acumulador + `dt = 1/60`.
- Cap ticks/frame (anti spiral-of-death).
- **Regla:** gameplay determinista solo en tick fijo; nunca en `Draw`.

## Content

- Oficial shipped bajo `Content/…` con `CopyToOutputDirectory` o MGCB.
- User data (`%LocalAppData%/GameName/`) para saves, UGC, settings — no escribir user content solo en `bin/`.

## Checklist bootstrap

- [ ] Proyecto compila `dotnet build` / `dotnet run`
- [ ] Una escena menú + cambio de escena
- [ ] Settings básicos (resolución) persistidos
- [ ] `.gitignore` con `bin/`, `obj/`
- [ ] (Opcional) `steam_appid.txt` + init fail-soft

**Referencia CB:** [`../01-ARQUITECTURA.md`](../01-ARQUITECTURA.md), `Core/ColorBlocksGame.cs`.
