#nullable enable
namespace ColorBlocks;

/// <summary>
/// Steam allows 10,000 leaderboards per app. With per-player-count boards (1–4):
/// Official reserve 20 levels × 4 = 80; workshop eligibility = (10000 − 80) / 4 = 2480.
/// </summary>
public static class LeaderboardQuota
{
    public const int SteamMaxLeaderboards = 10_000;
    public const int OfficialLevelReserve = 20;
    public const int PlayerCountBoards = 4;
    public const int OfficialBoardReserve = OfficialLevelReserve * PlayerCountBoards;
    public const int WorkshopEligibleLevelCap = (SteamMaxLeaderboards - OfficialBoardReserve) / PlayerCountBoards;
}
