using System.Collections.Generic;

namespace Game1_Monogame;

public sealed class RopeSnapshot
{
    public int NetworkId { get; init; }
    public int OwnerId { get; init; }
    public int StartPlayerNetworkId { get; init; }
    public int EndPlayerNetworkId { get; init; }
    public RopeGameplayMode RopeMode { get; init; }
    public List<NetworkVector2> NodePositions { get; init; } = new();
    public float Tension { get; init; }
    public bool IsTense { get; init; }
    public float PullIntensity { get; init; }
    public int PulledNodeCount { get; init; }
}
