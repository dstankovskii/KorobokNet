using KorobokNet.Engine;
using KorobokNet.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

/// <summary>Выбор уровня. Пройденные уровни зелёные, пройденные на 3 звезды - с медалью.</summary>
public sealed class LevelSelectScreen : Screen
{
    private readonly List<Button> _levels = [];
    private readonly Button _back;

    public LevelSelectScreen(KorobokGame game)
        : base(game)
    {
        _back = new Button(new RectangleF(40, 30, 150, 112), sprite: "back", onClick: () => Game.Show(new StartScreen(Game)));
        var count = Game.Session.Levels.Count;
        for (var i = 0; i < count; i++)
        {
            var number = i + 1;
            var col = i % 4;
            var row = i / 4;
            var rect = new RectangleF(330 + col * 340, 260 + row * 250, 240, 120);
            _levels.Add(new Button(rect, text: number.ToString(), onClick: () => Game.Show(new LevelScreen(Game, Game.Session.Levels[number - 1])))
            {
                TextSize = 56,
            });
        }
    }

    public override void Update(float dt, Input input)
    {
        _back.Update(input);
        for (var i = 0; i < _levels.Count; i++)
        {
            _levels[i].Sprite = Game.Session.Stars(i + 1) switch
            {
                3 => "button_medal",
                > 0 => "button_ok",
                _ => "button",
            };
            _levels[i].Update(input);
        }
        if (input.KeyPressed(Keys.Escape))
        {
            Game.Show(new StartScreen(Game));
        }
    }

    public override void Draw(Gfx g)
    {
        DrawSky(g);
        DrawTitle(g, "Выберите уровень:", 60);
        _back.Draw(g);
        g.Text(Game.Session.User?.Name ?? "", new Vector2(Gfx.Width - 50, 60), 44, Color.White, Align.Right);

        var stageNames = new[] { "Последовательность", "Условия", "Циклы" };
        for (var row = 0; row < 3; row++)
        {
            g.Text(stageNames[row], new Vector2(300, 205 + row * 250), 34, new Color(255, 255, 255, 220), Align.Left);
        }

        for (var i = 0; i < _levels.Count; i++)
        {
            var button = _levels[i];
            button.Draw(g);
            var stars = Game.Session.Stars(i + 1) ?? 0;
            for (var s = 0; s < 3; s++)
            {
                var area = RectangleF.FromCenter(new Vector2(button.Bounds.Center.X + (s - 1) * 52, button.Bounds.Bottom + 36), 46, 46);
                g.SpriteFit(s < stars ? "star_on" : "star_off", area, s < stars ? Color.White : Color.White * 0.5f);
            }
        }
    }
}
