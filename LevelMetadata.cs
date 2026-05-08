namespace Game1_Monogame;

public sealed class LevelMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public LevelMetadata()
    {
    }

    public LevelMetadata(string id, string name, string filePath)
    {
        Id = id;
        Name = name;
        FilePath = filePath;
    }

    public override string ToString() => $"{Name} ({Id})";
}
