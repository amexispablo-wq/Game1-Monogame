using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public interface IFocusable
{
    Rectangle Bounds { get; }
    bool IsEnabled { get; }
    bool CaptureHorizontalNavigation { get; }
    bool OnHorizontal(int direction);
    bool OnConfirm();
    void Update(InputManager input, InputNavigationService navigation, bool isFocused);
    void DrawFocusHighlight(SpriteBatch spriteBatch, Texture2D pixel, GameTime gameTime);
}
