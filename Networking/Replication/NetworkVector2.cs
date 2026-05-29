using Microsoft.Xna.Framework;

namespace ColorBlocks;

public readonly record struct NetworkVector2(float X, float Y)
{
    public static NetworkVector2 FromVector2(Vector2 value)
    {
        return new NetworkVector2(value.X, value.Y);
    }

    public Vector2 ToVector2()
    {
        return new Vector2(X, Y);
    }
}
