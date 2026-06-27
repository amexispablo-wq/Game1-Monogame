#nullable enable
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace ColorBlocks;

internal static class ContentResolver
{
    private static Microsoft.Xna.Framework.Game? _game;

    public static void Bind(Microsoft.Xna.Framework.Game game)
    {
        _game = game;
    }

    public static Song? TryLoadSong(string assetPath)
    {
        if (_game is null)
        {
            return null;
        }

        try
        {
            return _game.Content.Load<Song>(assetPath);
        }
        catch
        {
            return null;
        }
    }
}
