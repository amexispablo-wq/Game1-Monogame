using Microsoft.Xna.Framework;

namespace ColorBlocks;

public sealed class RopeNode
{
    public RopeNode(Vector2 position, bool isPinned = false)
    {
        Position = position;
        PreviousPosition = position;
        IsPinned = isPinned;
    }

    public Vector2 Position { get; set; }
    public Vector2 PreviousPosition { get; set; }
    public bool IsPinned { get; set; }
    public bool IsColliding { get; set; }
    public Vector2 LastCollisionNormal { get; set; }
    public Vector2 PullVelocityDelta { get; set; }
    public float PullWeight { get; set; }
    public Vector2 LastPullForce { get; set; }
}
