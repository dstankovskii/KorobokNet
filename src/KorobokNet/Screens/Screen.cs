using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Screens;

/// <summary>Экран игры (меню, уровень, редактор программы...). Рисует в виртуальном экране 1920x1080.</summary>
public abstract class Screen(KorobokGame game)
{
    protected KorobokGame Game { get; } = game;

    protected GameAssets Assets => Game.Assets;

    public virtual void OnEnter()
    {
    }

    public virtual void OnLeave()
    {
    }

    public abstract void Update(float dt, Input input);

    public abstract void Draw(Gfx g);

    /// <summary>Прямоугольник элемента по имени - на него указывают стрелки и "палец" в обучении.</summary>
    public virtual RectangleF? Anchor(string name) => null;

    /// <summary>Небо и песок без робота и растения: вертикальная полоска стартового фона, растянутая на экран.</summary>
    protected static void DrawSky(Gfx g)
    {
        var sprite = g.Assets.Sprite("background_start");
        var strip = new Rectangle(sprite.Source.X + 800, sprite.Source.Y, 20, sprite.Source.Height);
        g.Batch.Draw(sprite.Texture, new Rectangle(0, 0, Gfx.Width, Gfx.Height), strip, Color.White);
    }

    protected static void DrawTitle(Gfx g, string text, float y = 50) =>
        g.Text(text, new Vector2(Gfx.Width / 2f, y), 80, Color.White, Align.Center);
}
