namespace ColorBlocks;

public readonly record struct PlayerSnapshot(
    int NetworkId,
    int OwnerId,
    int PlayerIndex,
    PlayerId PlayerId,
    NetworkVector2 Position,
    NetworkVector2 Velocity,
    NetworkVector2 Acceleration,
    GameColor Color,
    PlayerState State,
    bool IsGrounded,
    bool IsFrozen,
    string CosmeticSkinId = "",
    byte[]? CosmeticSkinPixels = null);
