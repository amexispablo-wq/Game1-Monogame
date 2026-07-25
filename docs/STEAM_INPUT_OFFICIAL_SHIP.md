# Steam Input — plug-and-play (no layout pick)

Goal: **open game → pad works**. Players must not open Controller settings.

## Partner (required — you click)

Without this, friends see empty layouts + dead pad.

1. Steamworks → App `4796400` → **Steam Input**
2. Enable Steam Input + families: Xbox, Generic, PS4, PS5, Switch Pro, Steam Deck
3. **Steam Input Template** = **Gamepad** (Valve) — **not** Custom Bundled, **not** Keyboard+Mouse
4. **Save** → **Publish** (Steamworks Publish)
5. Verify on account **without Your Layouts**: Recommended shows **Gamepad** (auto). List must not be empty.
6. Friend: restart Steam → open Color Blocks → pad works with zero Controller clicks

Reinstall does **not** publish Recommended. Only Partner Publish does.

### Why Valve Gamepad

Emulates native XInput. Color Blocks already reads MonoGame `GamePad` + R3 restart. Steam auto-applies Recommended → zero clicks.

## Code (already in build)

Dead-live demotion: if Steam marks slot live but Move/digitals stay idle while XInput shows real activity (~0.75s), ownership drops to `GamepadBackend`. F3 shows `deadDemote=True`.

Soft-claim (handle, not live) already falls through to XInput.

## Official bundled (later, optional)

Custom glyphs / Steam actions / Official Gamepad name:

1. Depot Set Live: `Steam/steam_input_manifest.vdf` + `controller_gamepad.vdf` rev 8+
2. Partner Template → Custom Configuration Bundled → path `Steam\steam_input_manifest.vdf`
3. Save + Publish

Do **not** switch to Custom Bundled until Official VDF is verified — empty Official = friends pad dead again.

## Temporary friend workaround

Properties → Controller → Steam Input = **Disabled** (Xbox). Prefer Partner Gamepad Publish.
