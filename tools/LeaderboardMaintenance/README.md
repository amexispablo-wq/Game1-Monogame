# LeaderboardMaintenance

Double-click `LeaderboardMaintenance.exe` — no env vars to set.

**Never** put this folder/exe in the Steam game depot (it embeds your publisher Web API key).

## Build the exe once

From repo root:

```bat
Tools\LeaderboardMaintenance\Publish.bat
```

Output: `Tools\LeaderboardMaintenance\publish\LeaderboardMaintenance.exe`

Optional: copy `Content\OfficialLevels` next to the exe (or leave the repo layout so it finds `Content\OfficialLevels` automatically when run from the publish folder during development).

## What it does

1. Fetches top 2480 workshop + current boards  
2. **Shows a preview** of every board that would be reset or deleted  
3. Asks **Y/N** before applying anything  
4. On accept: reset/delete + save `maintenance-state.json`  

