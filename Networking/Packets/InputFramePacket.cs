namespace Game1_Monogame;

public sealed record InputFramePacket(InputFrame Frame) : INetworkPacket
{
    public NetworkPacketType PacketType => NetworkPacketType.InputFrame;
    public long Tick => Frame.Tick;
}
