#nullable enable

namespace ColorBlocks.Replay;

public static class ReplayInvalidation
{
  public static void OnLevelEdited(string levelId)
  {
    ReplayStorage.InvalidateBestReplay(levelId);
    SteamGhostService.InvalidateWorldRecordGhost(levelId);
  }
}
