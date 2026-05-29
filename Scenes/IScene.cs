using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ColorBlocks;

public interface IScene
{
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime, SpriteBatch spriteBatch);
    void OnExit()
    {
    }
}
