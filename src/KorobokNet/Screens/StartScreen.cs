using System.Diagnostics;
using KorobokNet.Core.Saves;
using KorobokNet.Engine;
using KorobokNet.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

/// <summary>Главное меню: выбор или создание игрока, обучение, звук.</summary>
public sealed class StartScreen : Screen
{
    private const int UsersPerPage = 12;

    private readonly List<Button> _menu = [];
    private readonly Button _play;
    private readonly Button _switchUser;
    private readonly Button _tutorial;
    private readonly Button _back;
    private readonly Button _sound;
    private readonly Button _results;
    private readonly Button _create;
    private readonly Button _prevPage;
    private readonly Button _nextPage;
    private readonly TextBox _name;
    private readonly List<(Button Select, Button Delete)> _userButtons = [];
    private Mode _mode = Mode.Menu;
    private string? _error;
    private int _page;
    private Dialog? _dialog;
    private (int, int, string?)? _userButtonsKey;

    public StartScreen(KorobokGame game)
        : base(game)
    {
        var x = Gfx.Width / 2f - 300;
        var y = 280f;
        Button MenuButton(string text, Action onClick)
        {
            var b = new Button(new RectangleF(x, y, 600, 112), text: text, onClick: onClick) { TextSize = 52 };
            _menu.Add(b);
            y += 140;
            return b;
        }

        _play = MenuButton("Играть", () => Game.Show(new LevelSelectScreen(Game)));
        MenuButton("Новый игрок", () => SetMode(Mode.NewUser));
        _switchUser = MenuButton("Сменить игрока", () => SetMode(Mode.Users));
        _tutorial = MenuButton("Как играть", () => Game.Show(new StoryScreen(Game, StoryScreen.Intro)));
        MenuButton("Выход", Game.Exit);

        _back = new Button(new RectangleF(40, 30, 150, 112), sprite: "back", onClick: () => SetMode(Mode.Menu));
        _sound = new Button(new RectangleF(Gfx.Width - 150, 30, 110, 110), onClick: Game.ToggleSound);
        _results = new Button(new RectangleF(40, Gfx.Height - 110, 520, 80), text: "Результаты учеников", onClick: OpenResults)
        {
            TextSize = 36,
            Visible = OperatingSystem.IsWindows(),
        };

        _name = new TextBox(RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 420), 800, 110), "Введите ваше имя...", GameSession.MaxNameLength)
        {
            OnSubmit = CreateUser,
        };
        _create = new Button(RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 650), 500, 112), text: "Начать", onClick: CreateUser) { TextSize = 52 };
        _prevPage = new Button(new RectangleF(120, 560, 110, 110), sprite: "left", onClick: () => _page--);
        _nextPage = new Button(new RectangleF(Gfx.Width - 230, 560, 110, 110), sprite: "right", onClick: () => _page++);
    }

    private enum Mode
    {
        Menu,
        NewUser,
        Users,
    }

    public override void Update(float dt, Input input)
    {
        if (_dialog is { IsOpen: true })
        {
            _dialog.Update(input);
            return;
        }

        _sound.Sprite = Game.Session.SoundOff ? "sound_off" : "sound";
        _sound.Update(input);

        switch (_mode)
        {
            case Mode.Menu:
                var hasUser = Game.Session.User is not null;
                _play.Enabled = hasUser;
                _tutorial.Enabled = hasUser;
                _switchUser.Enabled = Game.Session.Data.Users.Count > 0;
                foreach (var b in _menu)
                {
                    b.Update(input);
                }
                _results.Update(input);
                break;

            case Mode.NewUser:
                _name.Update(dt, input);
                _create.Update(input);
                _back.Update(input);
                break;

            case Mode.Users:
                RebuildUserButtons();
                foreach (var (select, delete) in _userButtons)
                {
                    delete.Update(input);
                    select.Update(input);
                }
                _prevPage.Update(input);
                _nextPage.Update(input);
                _back.Update(input);
                break;
        }

        if (input.KeyPressed(Keys.Escape) && _mode != Mode.Menu)
        {
            SetMode(Mode.Menu);
        }
    }

    public override void Draw(Gfx g)
    {
        g.SpriteFill("background_start", new RectangleF(0, 0, Gfx.Width, Gfx.Height));
        _sound.Draw(g);

        switch (_mode)
        {
            case Mode.Menu:
                var user = Game.Session.User;
                DrawTitle(g, user is null ? "КоробОК" : $"Привет, {user.Name}!", 70);
                foreach (var b in _menu)
                {
                    b.Draw(g);
                }
                _results.Draw(g);
                break;

            case Mode.NewUser:
                DrawTitle(g, "Как тебя зовут?", 200);
                _name.Draw(g);
                if (_error is not null)
                {
                    g.Text(_error, new Vector2(Gfx.Width / 2f, 500), 40, new Color(170, 30, 30), Align.Center, bold: true);
                }
                _create.Draw(g);
                _back.Draw(g);
                break;

            case Mode.Users:
                DrawTitle(g, "Выбери игрока", 70);
                foreach (var (select, delete) in _userButtons)
                {
                    select.Draw(g);
                    delete.Draw(g);
                }
                _prevPage.Draw(g);
                _nextPage.Draw(g);
                _back.Draw(g);
                break;
        }
        _dialog?.Draw(g);
        if (_dialog is { IsOpen: false })
        {
            _dialog = null;
        }
    }

    private void SetMode(Mode mode)
    {
        _mode = mode;
        _error = null;
        _page = 0;
        _name.Text = "";
        _name.Focused = true;
    }

    private void CreateUser()
    {
        var check = Game.Session.CheckNewName(_name.Text);
        _error = check switch
        {
            NameCheck.Empty => "Напиши своё имя",
            NameCheck.TooLong => "Слишком длинное имя",
            NameCheck.Taken => "Такой игрок уже есть — выбери его в списке",
            _ => null,
        };
        if (check != NameCheck.Ok)
        {
            return;
        }
        Game.Session.CreateUser(_name.Text);
        Game.Show(new StoryScreen(Game, StoryScreen.Intro));
    }

    private void RebuildUserButtons()
    {
        var users = Game.Session.Data.Users;
        var pages = Math.Max(1, (users.Count + UsersPerPage - 1) / UsersPerPage);
        _page = Math.Clamp(_page, 0, pages - 1);
        _prevPage.Visible = _page > 0;
        _nextPage.Visible = _page < pages - 1;

        // Кнопки пересоздаются только при изменениях, иначе потеряется состояние нажатия.
        var key = (_page, users.Count, Game.Session.Data.LastUser);
        if (_userButtonsKey == key)
        {
            return;
        }
        _userButtonsKey = key;
        _userButtons.Clear();
        var onPage = users.Skip(_page * UsersPerPage).Take(UsersPerPage).ToList();
        for (var i = 0; i < onPage.Count; i++)
        {
            var name = onPage[i].Name;
            var col = i % 3;
            var row = i / 3;
            var rect = new RectangleF(330 + col * 440, 230 + row * 190, 360, 120);
            var select = new Button(rect, text: name, onClick: () =>
            {
                Game.Session.SelectUser(name);
                SetMode(Mode.Menu);
            })
            {
                TextSize = 44,
                Selected = string.Equals(name, Game.Session.Data.LastUser, StringComparison.OrdinalIgnoreCase),
            };
            var delete = new Button(new RectangleF(rect.Right - 30, rect.Y - 30, 64, 64), sprite: "clear", onClick: () => AskDelete(name));
            _userButtons.Add((select, delete));
        }
    }

    private void AskDelete(string name) =>
        _dialog = new Dialog(
            $"Удалить игрока «{name}»?\nЕго программы и результаты будут стёрты.",
            ("Удалить", () => Game.Session.DeleteUser(name)),
            ("Отмена", () => { }));

    private void OpenResults()
    {
        Directory.CreateDirectory(GamePaths.ResultsDirectory);
        Process.Start(new ProcessStartInfo(GamePaths.ResultsDirectory) { UseShellExecute = true });
    }
}
