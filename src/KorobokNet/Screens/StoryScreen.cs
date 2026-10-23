using System.Collections;
using KorobokNet.Core;
using KorobokNet.Core.Programs;
using KorobokNet.Engine;
using KorobokNet.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

public enum Backdrop
{
    Start,
    Level,
    QuestLevel, // поле с клетками "?" из 5 уровня
    Program,
}

// В оригинале вместо демонстраций были ролики 3_ride, 6_program и 8_performer.
public enum Demo
{
    None,
    Ride,
    Program,
    Delete,
    Perform,
}

/// <summary>
/// Шаг обучения: табличка с текстом поверх экрана игры и демонстрация.
/// Если задан Target, табличка встаёт рядом с этим элементом и указывает на него хвостиком.
/// </summary>
public sealed record StoryStep(Backdrop Backdrop, string Text, string? Target = null, Demo Demo = Demo.None)
{
    // Где стоит табличка без Target; null - сверху по центру.
    public RectangleF? Panel { get; init; }

    public string? Program { get; init; }

    public bool ShowStars { get; init; }
}

/// <summary>
/// Обучение (в оригинале сцена begin_scene). Вместо скриншотов и роликов на фоне идут живые экраны игры,
/// так что после правок интерфейса обучение не надо переснимать.
/// </summary>
public sealed class StoryScreen : Screen
{
    public static readonly IReadOnlyList<StoryStep> Intro =
    [
        new(Backdrop.Start, "Из-за загрязнения окружающей среды на планете вымерли все растения. Однако учёные вывели новое растение, способное восстановить экологию Земли."),
        new(Backdrop.Start, "Для рассадки этого растения по всей планете был сконструирован робот с кодовым именем «Коробок»."),
        new(Backdrop.Level, "Нужно запрограммировать робота так, чтобы он доставил растение на плодородную почву.", Demo: Demo.Ride),
        new(Backdrop.QuestLevel, "Ровно половина клеток, отмеченных «?», заражена радиацией. Робот сканирует клетку, на которую смотрит. Какая из клеток заражена, заранее узнать нельзя. Через радиацию робот не может ехать и толкать ящики."),
        new(Backdrop.Level, "Чтобы написать команды для робота — жми на планшет.", Target: "tablet"),
        new(Backdrop.Program, "Здесь пишется программа.", Target: "program"),
        new(Backdrop.Program, "Справа отображается мини-карта.", Target: "minimap"),
        new(Backdrop.Program, "Снизу находится панель команд Коробка.", Target: "palette"),
        new(Backdrop.Program, "Новая команда вставляется после выделенного блока. Нажми на блок, чтобы выделить его. Длинную программу можно листать стрелками по бокам.", Target: "program")
        {
            Program = "L F F",
        },
        new(Backdrop.Program, "Чтобы удалить блок, наведи на него мышку и нажми на крестик.", Demo: Demo.Delete)
        {
            Program = "L F F",
            Panel = new RectangleF(215, 510, 1000, 140),
        },
        new(Backdrop.Program, "А эта кнопка удаляет все команды сразу.", Target: "clear") { Program = "L F F" },
        new(Backdrop.Program, "Нажимай на команды внизу — они добавятся в программу.", Demo: Demo.Program) { Panel = new RectangleF(215, 510, 1000, 140) },
        new(Backdrop.Program, "Нажми «Готово», чтобы вернуться на поле и запустить программу.", Target: "done") { Program = "L F" },
        new(Backdrop.Level, "Чтобы запустить программу, переведи переключатель в ON.", Target: "play"),
        new(Backdrop.Level, "Робот выполнит команды по порядку. Если ящик окажется на почве — вырастет растение!", Demo: Demo.Perform),
        new(Backdrop.Level, "За пройденный уровень даются звёзды, от 1 до 3. Чем короче программа, тем больше звёзд.", Target: "stars")
        {
            ShowStars = true,
        },
        new(Backdrop.Start, "Вот и все правила. Теперь помоги Коробку спасти планету!"),
    ];

    public static readonly IReadOnlyList<StoryStep> Finale =
    [
        new(Backdrop.Start, "Ты справился! Все растения рассажены."),
    ];

    private static readonly RectangleF DefaultPanel = new(360, 60, 1200, 260);
    private const float NavHeight = 80;
    private const float NavY = Gfx.Height - NavHeight - 12;

    // Таблички с хвостиком не должны залезать на кнопки "Назад" и "Далее".
    private static readonly RectangleF CalloutRoom = new(24, 24, Gfx.Width - 48, NavY - 40);

    private readonly IReadOnlyList<StoryStep> _steps;
    private readonly Coroutines _script = new();
    private readonly Input _backdropInput = new();
    private readonly Button _prev;
    private readonly Button _next;
    private readonly Button _skip;
    private readonly LevelDefinition _tutorial;
    private int _index;
    private Screen? _backdrop;
    private Vector2 _finger;
    private bool _fingerDown;
    private bool _fingerVisible;

    /// <param name="startIndex">С какого шага начать (для скриншот-тура).</param>
    public StoryScreen(KorobokGame game, IReadOnlyList<StoryStep> steps, int startIndex = 0)
        : base(game)
    {
        _steps = steps;
        _tutorial = Game.Session.Catalog.Tutorial;
        // Внизу по центру кнопкам ничего не мешает: планшет, палитра и мини-карта в других местах.
        var center = Gfx.Width / 2f;
        _prev = new Button(new RectangleF(center - 390, NavY, 280, NavHeight), text: "Назад", onClick: () => Go(_index - 1)) { TextSize = 42 };
        _next = Button.Primary(new RectangleF(center + 110, NavY, 280, NavHeight), "Далее", () => Go(_index + 1));
        // "Пропустить" специально бледная, чтобы обучение не пролистывали.
        _skip = new Button(new RectangleF(center + 430, NavY + 10, 200, NavHeight - 20), text: "Пропустить", onClick: Finish)
        {
            TextSize = 30,
            Fill = Color.Transparent,
            Border = Color.Transparent,
            TextColor = Color.White * 0.55f,
        };
        Go(startIndex);
    }

    private StoryStep Step => _steps[_index];

    public override void OnLeave()
    {
        _script.StopAll();
        _backdrop?.OnLeave();
    }

    public override void Update(float dt, Input input)
    {
        _script.Update(dt);
        if (_fingerVisible)
        {
            _backdropInput.Script(_finger, _fingerDown);
        }
        else
        {
            // Фон не реагирует на настоящие нажатия.
            _backdropInput.Script(new Vector2(-1000, -1000), false);
        }
        _backdrop?.Update(dt, _backdropInput);

        _prev.Visible = _index > 0;
        _skip.Visible = _index < _steps.Count - 1;
        _next.Text = _index < _steps.Count - 1 ? "Далее" : _steps == Intro ? "Начать!" : "Готово";
        _prev.Update(input);
        _next.Update(input);
        _skip.Update(input);

        if (input.KeyPressed(Keys.Right) || input.KeyPressed(Keys.Enter) || input.KeyPressed(Keys.Space))
        {
            Go(_index + 1);
        }
        else if (input.KeyPressed(Keys.Left) && _index > 0)
        {
            Go(_index - 1);
        }
        else if (input.KeyPressed(Keys.Escape))
        {
            Finish();
        }
    }

    public override void Draw(Gfx g)
    {
        if (_backdrop is null)
        {
            g.SpriteFill("background_start", new RectangleF(0, 0, Gfx.Width, Gfx.Height));
        }
        else
        {
            _backdrop.Draw(g);
        }

        if (Step.Target is { } anchor && _backdrop?.Anchor(anchor) is { } target)
        {
            Callout.Draw(g, Step.Text, target, CalloutRoom);
        }
        else
        {
            var panel = Step.Panel ?? DefaultPanel;
            if (_index == 0 && _steps == Intro && Game.Session.User is { } user)
            {
                g.Text($"Привет, {user.Name}!", new Vector2(panel.Center.X, panel.Y - 4), 60, Color.White, Align.Center);
                panel = panel.Offset(new Vector2(0, 80));
            }
            g.Panel(panel, Callout.Fill, Callout.Border, 26, 4);
            g.TextBlock(Step.Text, panel.Inflate(-36), 46, Callout.Ink, bold: true);
        }
        if (_fingerVisible)
        {
            DrawHand(g);
        }

        _prev.Draw(g);
        _next.Draw(g);
        _skip.Draw(g);
        g.Text($"{_index + 1} / {_steps.Count}", new Vector2(Gfx.Width / 2f, NavY + 18), 36, Color.White * 0.9f, Align.Center);
    }

    private void Go(int index)
    {
        if (index < 0)
        {
            return;
        }
        if (index >= _steps.Count)
        {
            Finish();
            return;
        }
        _index = index;
        _script.StopAll();
        _backdrop?.OnLeave();
        _fingerVisible = false;
        _fingerDown = false;
        _backdrop = CreateBackdrop(Step);
        if (Step.Demo != Demo.None)
        {
            _script.Start(RunDemo(Step.Demo));
        }
    }

    private Screen? CreateBackdrop(StoryStep step)
    {
        switch (step.Backdrop)
        {
            case Backdrop.Start:
                return null;
            case Backdrop.QuestLevel:
                return new LevelScreen(Game, Game.Session.Levels[4], RobotProgram.Empty);
            case Backdrop.Program:
                return new ProgramScreen(Game, _tutorial, demo: true, step.Program is null ? null : RobotProgram.Parse(step.Program));
            default:
                var program = step.Demo switch
                {
                    // Квадрат: робот возвращается на старт и едет по новой.
                    Demo.Ride => RobotProgram.Parse("F R F R F R F R"),
                    Demo.Perform => _tutorial.Solution ?? RobotProgram.Empty,
                    _ => RobotProgram.Empty,
                };
                var level = new LevelScreen(Game, _tutorial, program);
                if (step.ShowStars)
                {
                    level.PreviewResult(3);
                }
                return level;
        }
    }

    private void Finish()
    {
        if (_steps == Finale || Game.Session.User is null)
        {
            Game.Show(new StartScreen(Game));
        }
        else
        {
            Game.Show(new LevelSelectScreen(Game));
        }
    }

    private IEnumerator RunDemo(Demo demo)
    {
        _finger = new Vector2(Gfx.Width / 2f, Gfx.Height + 50);
        _fingerVisible = true;
        while (true)
        {
            yield return 0.8f;
            switch (demo)
            {
                case Demo.Program:
                    foreach (var key in new[] { "palette.left", "palette.forward" })
                    {
                        yield return MoveTo(key);
                        yield return Click();
                        yield return 0.4f;
                    }
                    yield return 2f;
                    yield return MoveTo("clear");
                    yield return Click();
                    break;

                case Demo.Delete:
                    // Наведение на блок показывает крестик, нажатие на крестик удаляет блок.
                    yield return MoveTo("last-block");
                    yield return 0.8f;
                    yield return MoveTo("last-block.delete");
                    yield return Click();
                    yield return 1f;
                    yield return MoveAway();
                    yield return 1f;
                    _backdrop?.OnLeave();
                    _backdrop = CreateBackdrop(Step);
                    break;

                case Demo.Ride:
                case Demo.Perform:
                    var level = (LevelScreen)_backdrop!;
                    yield return MoveTo("play");
                    yield return Click();
                    yield return MoveAway();
                    yield return (Func<bool>)(() => level.IsFinished);
                    yield return demo == Demo.Perform ? 4f : 1.2f;
                    // Новое поле для следующего круга демонстрации.
                    _backdrop?.OnLeave();
                    _backdrop = CreateBackdrop(Step);
                    break;
            }
        }
    }

    private IEnumerator MoveTo(string anchor)
    {
        if (_backdrop?.Anchor(anchor) is not { } target)
        {
            yield break;
        }
        yield return MoveTo(target.Center, 0.7f);
    }

    /// <summary>Рука, которая показывает, куда нажимать. При нажатии она прижимается, и под пальцем расходится круг.</summary>
    private void DrawHand(Gfx g)
    {
        var size = _fingerDown ? 108f : 120f;
        var area = new RectangleF(_finger.X - size * Icons.HandTip.X, _finger.Y - size * Icons.HandTip.Y, size, size);
        if (_fingerDown)
        {
            g.Circle(_finger, 34, Color.White * 0.35f);
            g.Circle(_finger, 22, Color.White * 0.5f);
        }
        var lift = _fingerDown ? new Vector2(3, 4) : new Vector2(9, 12);
        Icons.Draw(g, "hand", area.Offset(lift), Color.Black * 0.3f);
        Icons.Draw(g, "hand", area);
    }

    private IEnumerator MoveAway() => MoveTo(new Vector2(Gfx.Width - 200, Gfx.Height / 2f), 0.6f);

    private IEnumerator MoveTo(Vector2 to, float seconds)
    {
        var from = _finger;
        for (var t = 0f; t < seconds; t += Frame.Dt)
        {
            var k = MathHelper.SmoothStep(0, 1, t / seconds);
            _finger = Vector2.Lerp(from, to, k);
            yield return null;
        }
        _finger = to;
    }

    private IEnumerator Click()
    {
        _fingerDown = true;
        yield return 0.15f;
        _fingerDown = false;
        yield return 0.3f;
    }
}
