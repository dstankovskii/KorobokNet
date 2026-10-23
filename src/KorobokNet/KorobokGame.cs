using KorobokNet.Core;
using KorobokNet.Core.Saves;
using KorobokNet.Engine;
using KorobokNet.Screens;
using KorobokNet.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet;

/// <summary>
/// Окно игры. Всё рисуется в виртуальный экран 1920x1080 и масштабируется в окно с полями по краям.
/// Запускается на весь экран; F11 или Alt+Enter - переключение в окно и обратно (без смены видеорежима),
/// F12 - скриншот в Изображения\KorobokNet.
/// </summary>
public sealed class KorobokGame : Game
{
    private const float FadeSeconds = 0.18f;
    private const string Title = "КоробОК";
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    private readonly GameOptions _options;
    private readonly GraphicsDeviceManager _graphics;
    private ScreenshotTour? _tour;
    private string? _pendingShot;
    private readonly Input _input = new();
    private SpriteBatch _batch = null!;
    private RenderTarget2D _target = null!;
    private Screen? _screen;
    private Screen? _next;
    private float _fade;
    private Rectangle _viewport;

    public KorobokGame(GameOptions options)
    {
        _options = options;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = WindowWidth,
            PreferredBackBufferHeight = WindowHeight,
            HardwareModeSwitch = false,
            SynchronizeWithVerticalRetrace = true,
        };
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
    }

    public GameAssets Assets { get; private set; } = null!;
    public Audio Audio { get; private set; } = null!;
    public GameSession Session { get; private set; } = null!;
    public Gfx Gfx { get; private set; } = null!;

    /// <summary>Переход на другой экран с затемнением.</summary>
    public void Show(Screen screen)
    {
        _next = screen;
        if (_screen is null)
        {
            SwitchScreen();
        }
    }

    /// <summary>Переход на другой экран без затемнения.</summary>
    public void ShowImmediately(Screen screen)
    {
        _next = screen;
        _fade = 0;
        SwitchScreen();
    }

    public void ToggleSound()
    {
        Session.SoundOff = !Session.SoundOff;
        Audio.Muted = Session.SoundOff;
    }

    protected override void Initialize()
    {
        WindowTitle.Set(Window, Title);
        Window.TextInput += (_, e) => _input.OnTextInput(e.Character);
        if (_options.TourDirectory is null)
        {
            SetFullScreen(true);
        }
        base.Initialize();
    }

    /// <summary>Полный экран без смены видеорежима: окно без рамки на весь рабочий стол.</summary>
    private void SetFullScreen(bool fullScreen)
    {
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.IsFullScreen = fullScreen;
        _graphics.PreferredBackBufferWidth = fullScreen ? display.Width : WindowWidth;
        _graphics.PreferredBackBufferHeight = fullScreen ? display.Height : WindowHeight;
        _graphics.ApplyChanges();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _target = new RenderTarget2D(GraphicsDevice, Gfx.Width, Gfx.Height);
        Assets = new GameAssets(GraphicsDevice);
        Gfx = new Gfx(_batch, Assets);
        Audio = new Audio();
        if (_options.TourDirectory is not null && File.Exists(_options.SaveFile))
        {
            File.Delete(_options.SaveFile);
        }
        Session = new GameSession(new SaveStore(_options.SaveFile), new ResultLog(_options.ResultsDirectory), LevelLoader.LoadCatalog());
        Button.ClickSound = () => Audio.Play(Sfx.Click);

        if (_options.TourDirectory is not null)
        {
            Audio.Muted = true;
            Session.CreateUser("Тест");
            _tour = new ScreenshotTour(this, _options.TourDirectory);
            return;
        }

        Audio.Muted = Session.SoundOff;
        Audio.StartMusic();
        Show(new StartScreen(this));
    }

    protected override void UnloadContent()
    {
        Audio.Dispose();
        Assets.Dispose();
        _target.Dispose();
        _batch.Dispose();
    }

    protected override void Update(GameTime gameTime)
    {
        var dt = Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.1f);
        Frame.Dt = dt;
        _viewport = Letterbox();
        _input.Update(ToVirtual, IsActive);

        if (_input.KeyPressed(Keys.F11) || (_input.KeyPressed(Keys.Enter) && (_input.KeyDown(Keys.LeftAlt) || _input.KeyDown(Keys.RightAlt))))
        {
            SetFullScreen(!_graphics.IsFullScreen);
        }
        if (_input.KeyPressed(Keys.F12))
        {
            _pendingShot = Path.Combine(GamePaths.ScreenshotsDirectory, $"KorobokNet-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        }

        Audio.Update(dt);
        if (_next is not null)
        {
            _fade += dt / FadeSeconds;
            if (_fade >= 1)
            {
                SwitchScreen();
            }
        }
        else if (_fade > 0)
        {
            _fade = Math.Max(0, _fade - dt / FadeSeconds);
        }

        if (_next is null)
        {
            _screen?.Update(dt, _input);
        }
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(Color.Black);
        Gfx.Begin();
        _screen?.Draw(Gfx);
        if (_fade > 0)
        {
            Gfx.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Color.Black * Math.Clamp(_fade, 0, 1));
        }
        Gfx.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(new Color(20, 20, 24));
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        _batch.Draw(_target, _viewport, Color.White);
        _batch.End();
        base.Draw(gameTime);

        if (_tour is not null)
        {
            _pendingShot ??= _tour.Advance(Frame.Dt);
        }
        if (_pendingShot is not null)
        {
            SaveScreenshot(_pendingShot);
            _pendingShot = null;
        }
        if (_tour is { Finished: true })
        {
            Exit();
        }
    }

    private void SaveScreenshot(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var file = File.Create(path);
            _target.SaveAsPng(file, _target.Width, _target.Height);
        }
        catch (IOException)
        {
            // Скриншот не обязателен - игру из-за него не роняем.
        }
    }

    private void SwitchScreen()
    {
        _screen?.OnLeave();
        _screen = _next;
        _next = null;
        _screen?.OnEnter();
    }

    /// <summary>Прямоугольник окна, в который вписан виртуальный экран с сохранением пропорций.</summary>
    private Rectangle Letterbox()
    {
        var bounds = GraphicsDevice.PresentationParameters.Bounds;
        var scale = Math.Min((float)bounds.Width / Gfx.Width, (float)bounds.Height / Gfx.Height);
        var w = (int)(Gfx.Width * scale);
        var h = (int)(Gfx.Height * scale);
        return new Rectangle((bounds.Width - w) / 2, (bounds.Height - h) / 2, w, h);
    }

    private Vector2 ToVirtual(Vector2 window) =>
        _viewport.Width == 0
            ? window
            : new Vector2((window.X - _viewport.X) * Gfx.Width / _viewport.Width, (window.Y - _viewport.Y) * Gfx.Height / _viewport.Height);
}
