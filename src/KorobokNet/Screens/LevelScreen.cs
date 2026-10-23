using KorobokNet.Core;
using KorobokNet.Core.Programs;
using KorobokNet.Engine;
using KorobokNet.Ui;
using KorobokNet.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

/// <summary>
/// Уровень: поле с роботом, переключатель ON/OFF (запуск и сброс), планшет (редактор программы).
/// В режиме демонстрации (<see cref="IsDemo"/>) выполняет заданную программу и ничего не сохраняет.
/// </summary>
public sealed class LevelScreen : Screen
{
    private static readonly Dictionary<int, string> Intros = new()
    {
        [1] = "Доставь ящик с растением на плодородную почву.\nНапиши программу на планшете и нажми ON.",
        [5] = "Новое: условие.\nПоловина клеток «?» заражена радиацией. Условие проверяет клетку перед роботом: если там радиация — выполняется ветка «Да», иначе — ветка «Нет».",
        [9] = "Новое: цикл.\nЦикл повторяет команды внутри него несколько раз. Чтобы получить 3 звезды, используй цикл.",
    };

    private readonly LevelDefinition _level;
    private readonly RobotProgram? _demoProgram;
    private readonly Coroutines _routines = new();
    private readonly Button _back;
    private readonly Button _toggle;
    private readonly Button _tablet;
    private readonly Button _continue;
    private readonly Button _menu;
    private readonly Button _introOk;
    private BoardView _view = null!;
    private State _state;
    private int _stars;
    private float _starsTime;
    private string? _message;
    private string? _intro;

    public LevelScreen(KorobokGame game, LevelDefinition level, RobotProgram? demoProgram = null)
        : base(game)
    {
        _level = level;
        _demoProgram = demoProgram;
        _back = new Button(new RectangleF(40, 30, 150, 112), sprite: "back", onClick: GoBack);
        _toggle = new Button(new RectangleF(Gfx.Width - 330, 30, 290, 140), sprite: "button_stop", onClick: Toggle);
        _tablet = new Button(new RectangleF(Gfx.Width - 330, Gfx.Height - 250, 290, 220), sprite: "images", onClick: OpenProgram);
        var result = ResultPanel;
        _menu = new Button(new RectangleF(result.Center.X - 440, result.Bottom - 140, 360, 100), text: "Выйти в меню", onClick: GoBack) { TextSize = 42 };
        _continue = Button.Primary(new RectangleF(result.Center.X - 40, result.Bottom - 140, 480, 100), "Следующий уровень", NextLevel);
        _introOk = new Button(RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 760), 360, 100), text: "Понятно", onClick: () => _intro = null);
        Reset();
        if (!IsDemo && Intros.TryGetValue(level.Number, out var intro) && Game.Session.LoadProgram(level.Number).Instructions.Count == 0)
        {
            _intro = intro;
        }
    }

    private enum State
    {
        Idle,
        Running,
        Failed,
        Won,
    }

    public bool IsDemo => _demoProgram is not null;

    public bool IsRunning => _state == State.Running;

    public bool IsFinished => _state is State.Won or State.Failed;

    public override RectangleF? Anchor(string name) => name switch
    {
        "play" => _toggle.Bounds,
        "tablet" => _tablet.Bounds,
        "back" => _back.Bounds,
        "stars" => StarsArea,
        "board" => BoardView.ScreenBounds,
        _ => null,
    };

    /// <summary>Для обучения: показать звёзды без прохождения.</summary>
    public void ShowStars(int stars)
    {
        _stars = stars;
        _starsTime = 10;
    }

    /// <summary>Для скриншот-тура: окно итогов без прохождения и без записи в сохранение.</summary>
    public void PreviewResult(int stars)
    {
        _state = State.Won;
        _stars = stars;
        _starsTime = 10;
        _intro = null;
    }

    public override void OnLeave()
    {
        _routines.StopAll();
        Game.Audio.SetMotor(false);
    }

    public override void Update(float dt, Input input)
    {
        _routines.Update(dt);
        _view.Update(dt);
        _starsTime += dt;

        if (_intro is not null)
        {
            _introOk.Update(input);
            input.ConsumePointer();
            if (input.KeyPressed(Keys.Enter) || input.KeyPressed(Keys.Escape))
            {
                _intro = null;
            }
            return;
        }

        if (ShowResult)
        {
            _continue.Text = _level.Number < Game.Session.Levels.Count ? "Следующий уровень" : "Дальше";
            if (!_continue.Update(input))
            {
                _menu.Update(input);
            }
            input.ConsumePointer();
            if (input.KeyPressed(Keys.Enter) || input.KeyPressed(Keys.Space))
            {
                NextLevel();
            }
            else if (input.KeyPressed(Keys.Escape))
            {
                GoBack();
            }
            return;
        }

        _toggle.Sprite = _state == State.Idle ? "button_stop" : "button_play";
        _toggle.Enabled = _state != State.Won;
        _back.Enabled = !IsDemo;
        _tablet.Enabled = _state is not (State.Running or State.Won);

        _toggle.Update(input);
        _tablet.Update(input);
        _back.Update(input);

        if (!IsDemo)
        {
            if (input.KeyPressed(Keys.Escape))
            {
                GoBack();
            }
            else if (input.KeyPressed(Keys.Space))
            {
                Toggle();
            }
        }
    }

    public override void Draw(Gfx g)
    {
        g.SpriteFill("background", new RectangleF(0, 0, Gfx.Width, Gfx.Height));
        _view.Draw(g);

        _back.Draw(g);
        _toggle.Draw(g);
        _tablet.Draw(g);
        g.Text(_level.Number == 0 ? "Обучение" : $"Уровень: {_level.Number}", new Vector2(50, Gfx.Height - 100), 52, Color.White);

        if (IsDemo)
        {
            DrawStars(g, StarsArea, _starsTime);
        }

        if (_message is not null)
        {
            var panel = RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, Gfx.Height - 110), 1100, 130);
            g.Panel(panel, new Color(255, 248, 225, 240), new Color(60, 50, 40), 24, 3);
            g.TextBlock(_message, panel.Inflate(-24), 38, new Color(40, 35, 30), bold: true);
        }

        if (_intro is not null)
        {
            g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Color.Black * 0.45f);
            var panel = RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 520), 1200, 560);
            g.Panel(panel, new Color(255, 248, 225), new Color(60, 50, 40), 30, 4);
            g.TextBlock(_intro, new RectangleF(panel.X + 60, panel.Y + 40, panel.Width - 120, panel.Height - 200), 46, new Color(40, 35, 30), bold: true);
            _introOk.Draw(g);
        }

        if (ShowResult)
        {
            DrawResult(g);
        }
    }

    /// <summary>Запуск программы (ON) или сброс поля (OFF).</summary>
    public void Toggle()
    {
        switch (_state)
        {
            case State.Idle:
                Run();
                break;
            case State.Running:
            case State.Failed:
                Reset();
                break;
        }
    }

    // Окно итогов появляется не сразу, чтобы было видно, как выросло растение.
    private const float ResultDelay = 0.8f;

    private static RectangleF StarsArea => RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 110), 420, 150);

    private static RectangleF ResultPanel => RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, Gfx.Height / 2f), 1040, 580);

    private bool ShowResult => !IsDemo && _state == State.Won && _starsTime > ResultDelay;

    private RobotProgram Program => _demoProgram ?? Game.Session.LoadProgram(_level.Number);

    private void Run()
    {
        var program = Program;
        if (program.Instructions.Count == 0)
        {
            _message = "Программа пустая. Нажми на планшет, чтобы написать команды.";
            return;
        }
        _message = null;
        _state = State.Running;
        var runner = new ProgramRunner(_view.Board, program);
        _routines.Start(_view.Run(runner, won =>
        {
            if (!won)
            {
                _state = State.Failed;
                _message = IsDemo ? null : "Растение не доставлено. Нажми OFF, измени программу и попробуй ещё раз.";
                return;
            }
            _state = State.Won;
            _stars = IsDemo ? _level.Score(program.Stats) : Game.Session.RecordWin(_level, program, DateTime.Now);
            _starsTime = 0;
        }));
    }

    private void Reset()
    {
        _routines.StopAll();
        Game.Audio.SetMotor(false);
        var board = Board.Create(_level, Random.Shared);
        var seed = IsDemo ? 7 : Game.Session.DecorationSeed(_level.Number);
        _view = new BoardView(Assets, Game.Audio, board, seed);
        _state = State.Idle;
        _stars = 0;
        _message = null;
    }

    private void DrawResult(Gfx g)
    {
        var panel = ResultPanel;
        g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Color.Black * 0.5f);
        g.Panel(panel, new Color(255, 248, 225), new Color(60, 50, 40), 30, 4);
        var title = _level.Number == 0 ? "Обучение пройдено!" : $"Уровень {_level.Number} пройден!";
        g.Text(title, new Vector2(panel.Center.X, panel.Y + 40), 64, new Color(40, 35, 30), Align.Center, bold: true);
        DrawStars(g, RectangleF.FromCenter(new Vector2(panel.Center.X, panel.Y + 220), 420, 150), _starsTime - ResultDelay);
        var hint = _stars >= 3 ? "Отлично! Все звёзды собраны." : "Сделай программу короче — и звёзд будет больше.";
        g.Text(hint, new Vector2(panel.Center.X, panel.Y + 315), 36, new Color(90, 80, 70), Align.Center);
        _menu.Draw(g);
        _continue.Draw(g);
    }

    private void DrawStars(Gfx g, RectangleF area, float time)
    {
        if (_stars == 0)
        {
            return;
        }
        for (var i = 0; i < 3; i++)
        {
            // Звёзды "выпрыгивают" по очереди.
            var t = Math.Clamp((time - i * 0.3f) / 0.35f, 0f, 1f);
            if (t <= 0)
            {
                continue;
            }
            var pop = 1 + 0.3f * MathF.Sin(t * MathF.PI);
            var size = 120 * t * pop;
            var center = new Vector2(area.Center.X + (i - 1) * 140, area.Center.Y);
            g.SpriteFit(i < _stars ? "star_on" : "star_off", RectangleF.FromCenter(center, size, size));
        }
    }

    private void GoBack()
    {
        if (!IsDemo)
        {
            Game.Show(new LevelSelectScreen(Game));
        }
    }

    private void OpenProgram()
    {
        if (!IsDemo)
        {
            Game.Show(new ProgramScreen(Game, _level));
        }
    }

    private void NextLevel()
    {
        var levels = Game.Session.Levels;
        if (_level.Number < levels.Count)
        {
            Game.Show(new LevelScreen(Game, levels[_level.Number]));
        }
        else if (Game.Session.CompletedAll)
        {
            Game.Show(new StoryScreen(Game, StoryScreen.Finale));
        }
        else
        {
            Game.Show(new LevelSelectScreen(Game));
        }
    }
}
