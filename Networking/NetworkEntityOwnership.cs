namespace ColorBlocks;

public readonly record struct NetworkEntityOwnership(
    int NetworkId,
    int OwnerId,
    bool IsLocal,
    bool IsHostControlled)
{
    public bool IsRemote => !IsLocal;

    public static NetworkEntityOwnership LocalHost(int networkId)
    {
        return new NetworkEntityOwnership(networkId, NetworkOwners.HostOwnerId, true, true);
    }
}
