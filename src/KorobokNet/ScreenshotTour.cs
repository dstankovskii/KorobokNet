using KorobokNet.Screens;

namespace KorobokNet;

/// <summary>
/// Автоматический обход экранов для проверки графики: каждый экран показывается заданное время,
/// затем кадр сохраняется в PNG. Запуск: <c>KorobokNet --tour &lt;папка&gt;</c>.
/// </summary>
internal sealed class ScreenshotTour
{
    private readonly KorobokGame _game;
    private readonly string _directory;
    private readonly List<(string Name, Func<Screen> Create, float Seconds)> _steps;
    private int _index = -1;
    private float _timer;

    public ScreenshotTour(KorobokGame game, string directory)
    {
        _game = game;
        _directory = directory;
        var session = game.Session;
        var tutorial = session.Catalog.Tutorial;
        var level1 = session.Levels[0];
        var level9 = session.Levels[8];
        _steps =
        [
            ("start", () => new StartScreen(game), 1.5f),
            ("story-intro", () => new StoryScreen(game, StoryScreen.Intro), 1.5f),
            ("story-ride", () => new StoryScreen(game, StoryScreen.Intro, 2), 6f),
            ("story-program", () => new StoryScreen(game, StoryScreen.Intro, 11), 1.5f),
            ("story-delete", () => new StoryScreen(game, StoryScreen.Intro, 9), 2.2f),
            ("story-last", () => new StoryScreen(game, StoryScreen.Intro, StoryScreen.Intro.Count - 1), 1f),
            .. StoryScreen.Intro
                .Select((step, i) => (step, i))
                .Where(x => x.step.Target is not null)
                .Select(x => ($"story-{x.i + 1:00}-{x.step.Target}", (Func<Screen>)(() => new StoryScreen(game, StoryScreen.Intro, x.i)), 0.6f)),
            ("levels", () => new LevelSelectScreen(game), 1.5f),
            ("tutorial", () => new LevelScreen(game, tutorial), 1.5f),
            ("level-01", () => new LevelScreen(game, level1), 1.5f),
            ("level-01-running", () => new LevelScreen(game, level1, level1.Solution), 2.2f),
            ("level-01-won", () =>
            {
                var screen = new LevelScreen(game, level1);
                screen.PreviewResult(2);
                return screen;
            }, 1f),
            ("level-empty-program", () =>
            {
                var screen = new LevelScreen(game, level1, Core.Programs.RobotProgram.Empty);
                screen.Toggle();
                return screen;
            }, 1f),
            ("level-05-check", () =>
            {
                var screen = new LevelScreen(game, session.Levels[4], session.Levels[4].Solution);
                screen.Toggle();
                return screen;
            }, 1.1f),
            ("level-09", () => new LevelScreen(game, level9), 1.5f),
            ("program-empty", () => new ProgramScreen(game, level1), 1.5f),
            ("program-05", () => new ProgramScreen(game, session.Levels[4], initial: session.Levels[4].Solution), 1.5f),
            ("program-06", () => new ProgramScreen(game, session.Levels[5], initial: session.Levels[5].Solution), 1.5f),
            ("program-08", () => new ProgramScreen(game, session.Levels[7], initial: session.Levels[7].Solution), 1.5f),
            ("program-if-empty", () => new ProgramScreen(game, session.Levels[5], initial: Core.Programs.RobotProgram.Parse("F ?(|) L 2(F ?(R|))")), 1.5f),
            ("program-10", () => new ProgramScreen(game, session.Levels[9], initial: session.Levels[9].Solution), 1.5f),
            ("story-finale", () => new StoryScreen(game, StoryScreen.Finale), 1.5f),
            ("settings", () =>
            {
                var screen = new StartScreen(game);
                screen.OpenSettings();
                return screen;
            }, 1f),
        ];
    }

    public bool Finished => _index >= _steps.Count;

    /// <summary>Вызывается каждый кадр после отрисовки. Возвращает имя файла, если пора сохранить кадр.</summary>
    public string? Advance(float dt)
    {
        if (_index < 0)
        {
            Directory.CreateDirectory(_directory);
            Next();
            return null;
        }

        _timer += dt;
        if (Finished || _timer < _steps[_index].Seconds)
        {
            return null;
        }

        var file = Path.Combine(_directory, $"{_index + 1:00}-{_steps[_index].Name}.png");
        Next();
        return file;
    }

    private void Next()
    {
        _index++;
        _timer = 0;
        if (!Finished)
        {
            _game.ShowImmediately(_steps[_index].Create());
        }
    }
}
