using KorobokNet.Engine;
using KorobokNet.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

/// <summary>
/// Выбор уровня. Пройденные уровни зелёные, пройденные на 3 звезды - золотые.
/// Уровень открывается, когда пройден предыдущий.
/// </summary>
public sealed class LevelSelectScreen : Screen
{
    private readonly List<Button> _levels = [];
    private readonly Button _back;

    public LevelSelectScreen(KorobokGame game)
        : base(game)
    {
        _back = BackButton(() => Game.Show(new StartScreen(Game)));
        var count = Game.Session.Levels.Count;
        for (var i = 0; i < count; i++)
        {
            var number = i + 1;
            var col = i % 4;
            var row = i / 4;
            var rect = new RectangleF(330 + col * 340, 260 + row * 250, 240, 120);
            var unlocked = Game.Session.IsUnlocked(number);
            _levels.Add(new Button(rect, text: unlocked ? number.ToString() : null, onClick: () => Game.Show(new LevelScreen(Game, Game.Session.Levels[number - 1])))
            {
                TextSize = 56,
                Enabled = unlocked,
            });
        }
    }

    public override void Update(float dt, Input input)
    {
        _back.Update(input);
        for (var i = 0; i < _levels.Count; i++)
        {
            var (fill, border, text) = Game.Session.Stars(i + 1) switch
            {
                3 => (new Color(255, 204, 64), new Color(160, 110, 20), new Color(70, 45, 10)),
                > 0 => (new Color(118, 196, 72), new Color(45, 105, 30), Color.White),
                _ => (new Color(255, 248, 225), new Color(60, 50, 40), new Color(40, 35, 30)),
            };
            _levels[i].Fill = fill;
            _levels[i].Border = border;
            _levels[i].TextColor = text;
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
        DrawTitle(g, "Выбери уровень", 60);
        _back.Draw(g);

        var stageNames = new[] { "Последовательность", "Условия", "Циклы" };
        for (var row = 0; row < 3; row++)
        {
            g.Text(stageNames[row], new Vector2(300, 205 + row * 250), 34, new Color(255, 255, 255, 220), Align.Left);
        }

        for (var i = 0; i < _levels.Count; i++)
        {
            var button = _levels[i];
            button.Draw(g);
            if (!button.Enabled)
            {
                Icons.Draw(g, "lock", RectangleF.FromCenter(button.Bounds.Center, 72, 72), Color.White * 0.8f);
                continue;
            }
            var stars = Game.Session.Stars(i + 1) ?? 0;
            for (var s = 0; s < 3; s++)
            {
                var area = RectangleF.FromCenter(new Vector2(button.Bounds.Center.X + (s - 1) * 52, button.Bounds.Bottom + 36), 46, 46);
                g.SpriteFit(s < stars ? "star_on" : "star_off", area, s < stars ? Color.White : Color.White * 0.5f);
            }
        }
    }
}
