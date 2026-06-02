namespace ColorBlocks;

public sealed class EditorClipboardItem
{
    public EditorObjectKind Kind { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public GameColor Color { get; set; }
    public float RotationDegrees { get; set; }

    public EditorClipboardItem(EditorObjectKind kind, int x, int y, int width, int height, GameColor color, float rotationDegrees)
    {
        Kind = kind;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        Color = color;
        RotationDegrees = rotationDegrees;
    }
}
