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
    byte[]? CosmeticSkinPixels = null,
    float VisualRotation = 0f,
    float SpeedBuffRemaining = 0f,
    float SpeedBuffMultiplier = 1f,
    float JumpBuffRemaining = 0f,
    float JumpBuffMultiplier = 1f);
