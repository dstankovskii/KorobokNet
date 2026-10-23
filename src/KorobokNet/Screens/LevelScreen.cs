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
        [1] = "Доставь ящик с растением на плодородную почву.\nНапиши программу на планшете и переведи переключатель в ON.",
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
    private readonly Button _replay;
    private readonly Button _introOk;
    private BoardView _view = null!;
    private State _state;
    private int _stars;
    private float _starsTime;
    private float _failTime;
    private bool _preview;

    // Положение рычажка переключателя: 0 - OFF, 1 - ON. Двигается плавно.
    private float _knob;
    // Подсказка и кнопка, на которую она указывает.
    private (string Text, Button Target)? _message;
    private string? _intro;

    public LevelScreen(KorobokGame game, LevelDefinition level, RobotProgram? demoProgram = null)
        : base(game)
    {
        _level = level;
        _demoProgram = demoProgram;
        _back = BackButton(GoBack);
        _toggle = new Button(new RectangleF(Gfx.Width - 330, 30, 290, 140), onClick: Toggle) { Painter = DrawSwitch };
        _tablet = new Button(new RectangleF(Gfx.Width - 330, Gfx.Height - 250, 290, 220), onClick: OpenProgram) { Painter = DrawTablet };
        var result = ResultPanel;
        var buttonsY = result.Bottom - 140;
        _replay = new Button(new RectangleF(result.Center.X - 400, buttonsY, 370, 100), text: "Переиграть", onClick: Reset) { TextSize = 42 };
        _continue = Button.Primary(new RectangleF(result.Center.X + 30, buttonsY, 370, 100), "Далее", NextLevel);
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

    /// <summary>Окно итогов без прохождения и без записи в сохранение: для обучения и скриншот-тура.</summary>
    public void PreviewResult(int stars)
    {
        _state = State.Won;
        _stars = stars;
        _starsTime = 10;
        _intro = null;
        _preview = true;
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
        if (_state == State.Failed && !IsDemo)
        {
            _failTime += dt;
            if (_failTime > FailResetDelay)
            {
                ResetAfterFail();
            }
        }
        var switchedOn = _state == State.Idle ? 0f : 1f;
        _knob += Math.Clamp(switchedOn - _knob, -dt * 8, dt * 8);

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
            if (IsDemo)
            {
                return;
            }
            if (!_continue.Update(input))
            {
                _replay.Update(input);
            }
            input.ConsumePointer();
            if (input.KeyPressed(Keys.Enter) || input.KeyPressed(Keys.Space))
            {
                NextLevel();
            }
            else if (input.KeyPressed(Keys.R))
            {
                Reset();
            }
            else if (input.KeyPressed(Keys.Escape))
            {
                GoBack();
            }
            return;
        }

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
        DrawHorizon(g);
        _view.Draw(g);

        _back.Draw(g);
        _toggle.Draw(g);
        _tablet.Draw(g);

        if (_message is { } message)
        {
            Callout.Draw(g, message.Text, message.Target.Bounds, new RectangleF(24, 24, Gfx.Width - 48, Gfx.Height - 48), 38, 820);
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
            case State.Failed when !IsDemo:
                ResetAfterFail();
                break;
            case State.Running:
            case State.Failed:
                Reset();
                break;
        }
    }

    // Окно итогов появляется не сразу, чтобы было видно, как выросло растение.
    private const float ResultDelay = 0.8f;

    // После неудачи поле сбрасывается само, но не сразу: пусть будет видно, где робот застрял.
    private const float FailResetDelay = 1.5f;

    private static RectangleF ResultPanel => RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, Gfx.Height / 2f), 1040, 580);

    private static RectangleF StarsArea => RectangleF.FromCenter(new Vector2(ResultPanel.Center.X, ResultPanel.Y + 220), 420, 150);

    /// <summary>Полоска неба у верхнего края, цвета взяты с заставки. Без неё песок кажется стеной, а не землёй.</summary>
    private static void DrawHorizon(Gfx g)
    {
        var sky = new RectangleF(0, 0, Gfx.Width, 110);
        var horizon = new Color(254, 230, 227);
        g.Gradient(sky, new Color(212, 192, 190), horizon);
        g.Gradient(new RectangleF(0, sky.Bottom, Gfx.Width, 60), horizon, Color.Transparent);
    }

    /// <summary>Переключатель: выключен, пока программа не запущена; включён, пока робот едет и до сброса.</summary>
    private void DrawSwitch(Gfx g, RectangleF r, Color tint) => Switch.Draw(g, r, _knob, tint);

    /// <summary>Значок планшета: экран с двумя блоками программы.</summary>
    private static void DrawTablet(Gfx g, RectangleF r, Color tint)
    {
        var body = RectangleF.FromCenter(r.Center, r.Width * 0.92f, r.Height * 0.86f);
        var screen = body.Inflate(-16);
        g.RoundRect(body.Offset(new Vector2(0, 8)), 30, Gfx.Tint(Color.Black * 0.3f, tint));
        g.RoundRect(body, 30, Gfx.Tint(new Color(44, 48, 56), tint));
        g.RoundRect(screen, 14, Gfx.Tint(new Color(96, 178, 226), tint));
        g.Circle(new Vector2(body.Center.X, body.Y + 8), 3, Gfx.Tint(new Color(90, 96, 108), tint));

        var height = screen.Height * 0.66f;
        var width = height * 0.8f;
        var y = screen.Center.Y - height / 2;
        var left = new RectangleF(screen.X + screen.Width * 0.3f - width / 2, y, width, height);
        var right = new RectangleF(screen.X + screen.Width * 0.7f - width / 2, y, width, height);
        g.Line(left.Center, right.Center, 5, Gfx.Tint(new Color(40, 40, 40), tint));
        Blocks.Draw(g, left, Blocks.Control, "start", tint);
        Blocks.Draw(g, right, Blocks.Action, "forward", tint);
    }

    private bool ShowResult => (!IsDemo || _preview) && _state == State.Won && _starsTime > ResultDelay;

    private RobotProgram Program => _demoProgram ?? Game.Session.LoadProgram(_level.Number);

    private void Run()
    {
        var program = Program;
        if (program.Instructions.Count == 0)
        {
            _message = ("Программа пустая. Нажми на планшет, чтобы написать команды.", _tablet);
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
                _failTime = 0;
                return;
            }
            _state = State.Won;
            _stars = IsDemo ? _level.Score(program.Stats) : Game.Session.RecordWin(_level, program);
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
        _preview = false;
        _message = null;
    }

    private void ResetAfterFail()
    {
        Reset();
        _message = ("Растение не доставлено. Измени программу на планшете и попробуй ещё раз.", _tablet);
    }

    private void DrawResult(Gfx g)
    {
        var panel = ResultPanel;
        g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Color.Black * 0.5f);
        g.Panel(panel, new Color(255, 248, 225), new Color(60, 50, 40), 30, 4);
        var title = IsDemo ? "Уровень пройден!" : _level.Number == 0 ? "Обучение пройдено!" : $"Уровень {_level.Number} пройден!";
        g.Text(title, new Vector2(panel.Center.X, panel.Y + 40), 64, new Color(40, 35, 30), Align.Center, bold: true);
        DrawStars(g, StarsArea, _starsTime - ResultDelay);
        if (IsDemo)
        {
            // В обучении под звёздами встаёт табличка с объяснением.
            return;
        }
        var hint = _stars >= 3 ? "Отлично! Все звёзды собраны." : "Сделай программу короче — и звёзд будет больше.";
        g.Text(hint, new Vector2(panel.Center.X, panel.Y + 315), 36, new Color(90, 80, 70), Align.Center);
        _replay.Draw(g);
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
