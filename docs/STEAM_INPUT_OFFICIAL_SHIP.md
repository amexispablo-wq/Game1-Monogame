# Steam Input — Official layout ship (do now)

`publish.bat` already staged **revision 5** into ContentBuilder.

Verified:
- `ContentBuilder\content\Steam\controller_gamepad.vdf` → `"revision" "5"`, 2× `"analog"` Move bindings
- SHA256: `086B004C0657D42FF13EF043B69664DBAC1689C6C0086F4361181038DF4D2FE0`

## You must click (agent cannot)

### A) Upload + Set Live

```
cd C:\Users\amexi\Desktop\sdk\tools\ContentBuilder\builder
steamcmd.exe
login <user> <pass>
run_app_build C:\Users\amexi\Desktop\sdk\tools\ContentBuilder\scripts\app_build_4796400.vdf
```

Then Steamworks → Builds → **Set Live** on default/beta.

### B) Partner Save + Publish

1. Steamworks → App 4796400 → Steam Input
2. Custom Configuration (Bundled) → path `Steam\steam_input_manifest.vdf`
3. Opt in Xbox, PlayStation, Generic (and others you support)
4. **Save** → top **Publish** Partner changes

### C) Clean-account verify (critical)

Your **YOUR LAYOUTS / Molleja** layout hides broken Official. Test without it:

1. Alt Steam account **or** delete Color Blocks entry under Your Layouts
2. Properties → Controller → apply **RECOMMENDED → Official Gamepad**
3. Open Official → A=Jump, stick=Move must show
4. In-game F3: tilt stick → `Move≠0`

### D) Tell friends

1. Update game
2. Controller → Recommended → Official Gamepad (apply)
3. Steam Input = Enabled
4. If stuck on old personal layout → delete it / re-apply Official

## Success

Fresh install with no Your Layouts → Official Recommended has real bindings → pad works.
