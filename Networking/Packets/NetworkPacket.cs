namespace ColorBlocks;

public enum NetworkPacketType
{
    InputFrame,
    GameSnapshot,
    SessionState
}

public interface INetworkPacket
{
    NetworkPacketType PacketType { get; }
    long Tick { get; }
}
