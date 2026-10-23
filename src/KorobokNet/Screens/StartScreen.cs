using KorobokNet.Core.Saves;
using KorobokNet.Engine;
using KorobokNet.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

/// <summary>Главное меню и настройки: игроки, музыка и звуки.</summary>
public sealed class StartScreen : Screen
{
    private const int UsersPerPage = 12;

    private static readonly Color Ink = new(40, 35, 30);

    private readonly List<Button> _menu = [];
    private readonly List<Button> _settings = [];
    private readonly Button _tutorial;
    private readonly Button _switchUser;
    private readonly Button _back;
    private readonly Button _create;
    private readonly Button _prevPage;
    private readonly Button _nextPage;
    private readonly TextBox _name;
    private readonly List<(Button Select, Button Delete)> _userButtons = [];
    private Mode _mode = Mode.Menu;
    // Куда вернёт кнопка "назад" из списка игроков и ввода имени: в меню или в настройки.
    private Mode _backTo = Mode.Menu;
    private float _musicKnob;
    private float _soundKnob;
    private string? _error;
    private int _page;
    private Dialog? _dialog;
    private (int, int, string?)? _userButtonsKey;

    public StartScreen(KorobokGame game)
        : base(game)
    {
        var y = 340f;
        Button Add(List<Button> column, string? text, Action onClick)
        {
            var b = new Button(new RectangleF(Gfx.Width / 2f - 300, y, 600, 112), text: text, onClick: onClick) { TextSize = 52 };
            column.Add(b);
            y += 140;
            return b;
        }

        Add(_menu, "Уровни", Play);
        _tutorial = Add(_menu, "Правила", () => Game.Show(new StoryScreen(Game, StoryScreen.Intro)));
        Add(_menu, "Настройки", OpenSettings);

        y = 280;
        Add(_settings, "Новый игрок", () => Open(Mode.NewUser));
        _switchUser = Add(_settings, "Сменить игрока", () => Open(Mode.Users));
        Add(_settings, null, Game.ToggleMusic).Painter = SettingRow("Музыка", "music", () => _musicKnob);
        Add(_settings, null, Game.ToggleSound).Painter = SettingRow("Звуки", "sound", () => _soundKnob);
        _musicKnob = Game.Session.MusicOff ? 0 : 1;
        _soundKnob = Game.Session.SoundOff ? 0 : 1;

        _back = BackButton(Back);

        _name = new TextBox(RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 420), 800, 110), "Введи своё имя...", GameSession.MaxNameLength)
        {
            OnSubmit = CreateUser,
        };
        _create = new Button(RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, 650), 500, 112), text: "Начать", onClick: CreateUser) { TextSize = 52 };
        _prevPage = new Button(new RectangleF(120, 560, 110, 110), onClick: () => _page--) { Painter = Icons.RoundButton(Icons.DarkButton, "chevron_left") };
        _nextPage = new Button(new RectangleF(Gfx.Width - 230, 560, 110, 110), onClick: () => _page++) { Painter = Icons.RoundButton(Icons.DarkButton, "chevron_right") };
    }

    private enum Mode
    {
        Menu,
        Settings,
        NewUser,
        Users,
    }

    public void OpenSettings() => SetMode(Mode.Settings);

    public override void Update(float dt, Input input)
    {
        _musicKnob = MoveKnob(_musicKnob, !Game.Session.MusicOff, dt);
        _soundKnob = MoveKnob(_soundKnob, !Game.Session.SoundOff, dt);

        if (_dialog is { IsOpen: true })
        {
            _dialog.Update(input);
            return;
        }

        switch (_mode)
        {
            case Mode.Menu:
                _tutorial.Enabled = Game.Session.User is not null;
                foreach (var b in _menu)
                {
                    b.Update(input);
                }
                break;

            case Mode.Settings:
                _switchUser.Enabled = Game.Session.Data.Users.Count > 0;
                foreach (var b in _settings)
                {
                    b.Update(input);
                }
                _back.Update(input);
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
            Back();
        }
    }

    public override void Draw(Gfx g)
    {
        g.SpriteFill("background_start", new RectangleF(0, 0, Gfx.Width, Gfx.Height));

        switch (_mode)
        {
            case Mode.Menu:
                var user = Game.Session.User;
                DrawTitle(g, user is null ? "КоробОК" : $"Привет, {user.Name}!", 70);
                foreach (var b in _menu)
                {
                    b.Draw(g);
                }
                break;

            case Mode.Settings:
                DrawTitle(g, "Настройки", 70);
                foreach (var b in _settings)
                {
                    b.Draw(g);
                }
                _back.Draw(g);
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

    /// <summary>Строка настроек: значок, название и переключатель. Нажать можно на всю строку.</summary>
    private static Action<Gfx, RectangleF, Color> SettingRow(string label, string icon, Func<float> knob) => (g, r, tint) =>
    {
        g.Panel(r, Gfx.Tint(new Color(255, 248, 225), tint), new Color(60, 50, 40), 18, 3);
        Icons.Draw(g, icon, RectangleF.FromCenter(new Vector2(r.X + 70, r.Center.Y), 64, 64), Ink);
        g.TextBlock(label, new RectangleF(r.X + 130, r.Y, r.Width - 330, r.Height), 52, Ink, bold: true, align: Align.Left);
        Switch.Draw(g, new RectangleF(r.Right - 190, r.Center.Y - 40, 170, 80), knob(), tint);
    };

    private static float MoveKnob(float knob, bool on, float dt) => knob + Math.Clamp((on ? 1f : 0f) - knob, -dt * 8, dt * 8);

    private void Play()
    {
        if (Game.Session.User is null)
        {
            Open(Mode.NewUser);
        }
        else
        {
            Game.Show(new LevelSelectScreen(Game));
        }
    }

    private void Open(Mode mode)
    {
        _backTo = _mode;
        SetMode(mode);
    }

    private void Back() => SetMode(_mode == Mode.Settings ? Mode.Menu : _backTo);

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
            var delete = new Button(new RectangleF(rect.Right - 30, rect.Y - 30, 64, 64), onClick: () => AskDelete(name))
            {
                Painter = Icons.RoundButton(Icons.RedButton, "cross"),
            };
            _userButtons.Add((select, delete));
        }
    }

    private void AskDelete(string name) =>
        _dialog = new Dialog(
            $"Удалить игрока «{name}»?\nЕго программы и звёзды будут стёрты.",
            ("Удалить", () => Game.Session.DeleteUser(name)),
            ("Отмена", () => { }));
}
