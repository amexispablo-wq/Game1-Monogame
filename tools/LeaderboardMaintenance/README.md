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

1. Top 2480 workshop by unique subscriptions  
2. Reset official LBs when level `version` changed  
3. Reset workshop LBs when Steam `time_updated` changed  
4. Delete workshop LBs outside top 2480  
5. Delete leftover `_v*` boards  

State: `maintenance-state.json` next to the exe.
