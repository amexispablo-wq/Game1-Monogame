namespace Game1_Monogame;

public interface INetworkEntity
{
    int NetworkId { get; }
    int OwnerId { get; }
    bool IsLocal { get; }
    bool IsRemote { get; }
    bool IsHostControlled { get; }
}
