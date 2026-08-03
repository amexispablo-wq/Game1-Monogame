# Leaderboard maintenance (local tool)

Color Blocks uses Steam leaderboards with a hard app limit of **10,000** boards.

## Quota (with player-count boards)

| Bucket | Levels | Boards (×4 p-count) |
|--------|--------|---------------------|
| Official reserve | 20 | 80 |
| Workshop (top by unique subscriptions) | 2480 | 9920 |
| **Total** | | **10,000** |

Board names are **stable** (no `_v{version}`):

- Official: `official_{stem}_p{1-4}_f4`
- Workshop: `workshop_{publishedFileId}_p{1-4}_f4`

## Game (no server)

- Workshop eligibility: Steam UGC query ranked by unique subscriptions (top 2480), cached under `%LocalAppData%/Color Blocks/Cache/workshop-leaderboard-top.json`.
- Official: always eligible.
- Browse / WR peek: `FindLeaderboard` only (does not create boards).
- Score upload: `FindOrCreate` only when eligible.
- Updating a Workshop item does **not** reset LBs from the game. Run the maintenance tool after content changes (or on a schedule).

## Maintenance tool (your PC)

→ [`Tools/LeaderboardMaintenance/`](../Tools/LeaderboardMaintenance/)

Requires the local tool [`Tools/LeaderboardMaintenance`](../Tools/LeaderboardMaintenance/) on **your PC** (publisher key baked into that tool — never ship the tool with the game).

Each run:

1. Refresh top 2480 workshop IDs
2. Reset official boards when official `version` changed
3. Reset workshop boards when Steam `time_updated` changed
4. Delete workshop boards outside the top 2480
5. Delete leftover `_v*` boards from older naming

Run manually or schedule with Windows Task Scheduler.
