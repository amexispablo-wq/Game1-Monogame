namespace ColorBlocks;

public sealed record GameSnapshotPacket(GameSnapshot Snapshot) : INetworkPacket
{
    public NetworkPacketType PacketType => NetworkPacketType.GameSnapshot;
    public long Tick => Snapshot.Tick;
}
