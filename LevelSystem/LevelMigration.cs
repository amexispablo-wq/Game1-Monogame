namespace ColorBlocks;

/// <summary>
/// Compatibility entry point retained for older callers.
/// User-data migration now owns all persistence migration.
/// </summary>
internal static class LevelMigration
{
    public static void RunIfNeeded() => UserDataMigration.RunIfNeeded();
}
