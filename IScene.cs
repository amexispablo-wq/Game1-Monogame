using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Game1_Monogame;

public interface IScene
{
    void Update(GameTime gameTime);
    void Draw(GameTime gameTime, SpriteBatch spriteBatch);
    void OnExit()
    {
    }
}
