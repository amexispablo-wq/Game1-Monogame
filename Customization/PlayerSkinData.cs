#nullable enable
using System;

namespace ColorBlocks;

public sealed class PlayerSkinData
{
    public const int GridSize = 16;

    public bool[] Pixels { get; private set; } = CreateEmptyPixels();

    public static bool[] CreateEmptyPixels()
    {
        return new bool[GridSize * GridSize];
    }

    public PlayerSkinData Clone()
    {
        var clone = new PlayerSkinData();
        Array.Copy(Pixels, clone.Pixels, Pixels.Length);
        return clone;
    }

  public void CopyFrom(PlayerSkinData other)
  {
    Array.Copy(other.Pixels, Pixels, Pixels.Length);
  }

  public void Clear()
  {
    Array.Clear(Pixels, 0, Pixels.Length);
  }

    public bool GetPixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= GridSize || y >= GridSize)
        {
            return false;
        }

        return Pixels[(y * GridSize) + x];
    }

    public void SetPixel(int x, int y, bool painted)
    {
        if (x < 0 || y < 0 || x >= GridSize || y >= GridSize)
        {
            return;
        }

        Pixels[(y * GridSize) + x] = painted;
    }
}
