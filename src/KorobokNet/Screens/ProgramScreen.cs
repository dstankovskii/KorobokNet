using KorobokNet.Core;
using KorobokNet.Core.Editing;
using KorobokNet.Core.Programs;
using KorobokNet.Engine;
using KorobokNet.Ui;
using KorobokNet.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace KorobokNet.Screens;

/// <summary>
/// Планшет: составление программы. Сверху - лента блоков с курсором, справа - мини-карта,
/// снизу - палитра команд. Программа сохраняется при каждом изменении.
/// </summary>
public sealed class ProgramScreen : Screen
{
    private const float CellWidth = 118;
    private const float CellHeight = 148;
    private const float PitchX = 140;
    private const float PitchY = 200;
    private const float DragThreshold = 12;

    private static readonly Color Background = new(96, 178, 226);
    private static readonly Color LinkColor = new(40, 40, 40);
    private static readonly RectangleF ProgramArea = new(30, 160, 1360, 560);
    private static readonly RectangleF MinimapArea = new(1450, 50, 430, 430);
    private static readonly RectangleF PaletteArea = new(20, 735, 1010, 250);

    private readonly LevelDefinition _level;
    private readonly bool _demo;
    private readonly ProgramEditor _editor;
    private readonly Dictionary<string, Button> _palette = [];
    private readonly Dictionary<string, string> _paletteLabels = [];
    private readonly List<Button> _controls = [];
    private readonly Button _done;
    private readonly Button _clear;
    private readonly Button _backspace;
    private readonly Button _left;
    private readonly Button _right;
    private readonly List<Button> _countButtons = [];
    private ProgramLayout _layout;
    private float _scroll;
    private float _targetScroll;
    private Vector2? _pressStart;
    private float _pressScroll;
    private bool _dragging;
    private EditLoop? _countPicker;
    private string? _toast;
    private float _toastLeft;
    private Dialog? _dialog;

    /// <param name="demo">Режим обучения: программа не сохраняется, кнопка "Готово" не работает.</param>
    /// <param name="initial">Начальная программа для режима обучения.</param>
    public ProgramScreen(KorobokGame game, LevelDefinition level, bool demo = false, RobotProgram? initial = null)
        : base(game)
    {
        _level = level;
        _demo = demo;
        _editor = ProgramEditor.FromProgram(initial ?? (demo ? RobotProgram.Empty : Game.Session.LoadProgram(level.Number)));
        _layout = ProgramLayout.Build(_editor);
        _editor.Changed += OnChanged;

        _done = Button.Primary(new RectangleF(40, 40, 280, 90), "Готово", GoBack);

        (string Key, string Sprite, string Label, Action Action)[] palette =
        [
            ("forward", "move_forward", "вперёд", () => AddCommand(Command.Forward)),
            ("left", "move_left", "влево", () => AddCommand(Command.TurnLeft)),
            ("right", "move_right", "вправо", () => AddCommand(Command.TurnRight)),
            ("back", "move_backward", "назад", () => AddCommand(Command.Back)),
            ("if", "if", "условие", AddIf),
            ("loop", "end_cycle", "цикл", AddLoop),
        ];
        for (var i = 0; i < palette.Length; i++)
        {
            var (key, sprite, label, action) = palette[i];
            var rect = new RectangleF(PaletteArea.X + 25 + i * 165, PaletteArea.Y + 60, 140, 175);
            _palette[key] = new Button(rect, sprite: sprite, onClick: action);
            _paletteLabels[key] = label;
        }

        _clear = new Button(new RectangleF(1470, 560, 140, 140), sprite: "clear", onClick: AskClear);
        _left = new Button(new RectangleF(1680, 560, 140, 140), sprite: "left", onClick: () => _editor.MoveLeft());
        _backspace = new Button(new RectangleF(1470, 760, 140, 140), sprite: "backspace", onClick: Backspace);
        _right = new Button(new RectangleF(1680, 760, 140, 140), sprite: "right", onClick: () => _editor.MoveRight());
        _controls.AddRange([_clear, _left, _backspace, _right]);

        for (var n = ProgramEditor.MinLoopCount; n <= ProgramEditor.MaxLoopCount; n++)
        {
            var count = n;
            _countButtons.Add(new Button(default, text: n.ToString(), onClick: () => SetLoopCount(count)) { TextSize = 50 });
        }
        ScrollToCursor(immediate: true);
    }

    public ProgramEditor Editor => _editor;

    public override RectangleF? Anchor(string name) => name switch
    {
        "program" => ProgramArea,
        "minimap" => MinimapArea,
        "palette" => PaletteArea,
        "clear" => _clear.Bounds,
        "backspace" => _backspace.Bounds,
        "left" => _left.Bounds,
        "right" => _right.Bounds,
        "back" or "done" => _done.Bounds,
        _ when name.StartsWith("palette.", StringComparison.Ordinal) && _palette.TryGetValue(name["palette.".Length..], out var b) => b.Bounds,
        _ => null,
    };

    public override void Update(float dt, Input input)
    {
        _toastLeft -= dt;
        _scroll = MathHelper.Lerp(_scroll, _targetScroll, Math.Min(1, dt * 12));

        if (_dialog is { IsOpen: true })
        {
            _dialog.Update(input);
            return;
        }
        _dialog = null;

        if (_countPicker is not null)
        {
            UpdateCountPicker(input);
            return;
        }

        _done.Update(input);
        foreach (var button in _palette.Values)
        {
            button.Update(input);
        }
        foreach (var button in _controls)
        {
            button.Update(input);
        }
        UpdateProgramArea(input);

        if (!_demo)
        {
            if (input.KeyPressed(Keys.Escape))
            {
                GoBack();
            }
            else if (input.KeyPressed(Keys.Left))
            {
                _editor.MoveLeft();
            }
            else if (input.KeyPressed(Keys.Right))
            {
                _editor.MoveRight();
            }
            else if (input.KeyPressed(Keys.Back) || input.KeyPressed(Keys.Delete))
            {
                Backspace();
            }
            else if (input.KeyPressed(Keys.Up))
            {
                AddCommand(Command.Forward);
            }
            else if (input.KeyPressed(Keys.Down))
            {
                AddCommand(Command.Back);
            }
        }
    }

    public override void Draw(Gfx g)
    {
        g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Background);
        _done.Draw(g);
        g.Text("Программа сохраняется автоматически", new Vector2(_done.Bounds.Right + 30, _done.Bounds.Y + 26), 32, Color.White * 0.8f);

        DrawProgram(g);
        DrawMinimap(g);

        g.RoundRect(PaletteArea, 16, new Color(140, 200, 238));
        foreach (var (key, button) in _palette)
        {
            g.Text(_paletteLabels[key], new Vector2(button.Bounds.Center.X, PaletteArea.Y + 12), 34, new Color(70, 70, 80), Align.Center);
            button.Draw(g);
            // Недоступную команду всё равно можно нажать, тогда покажем, почему нельзя. Рисуем её бледнее.
            if ((key == "if" && !_editor.CanInsertIf) || (key == "loop" && !_editor.CanInsertLoop))
            {
                g.RoundRect(button.Bounds, 12, new Color(140, 200, 238) * 0.65f);
            }
        }
        foreach (var button in _controls)
        {
            button.Draw(g);
        }

        var bar = new RectangleF(0, Gfx.Height - 80, Gfx.Width, 80);
        g.Rect(bar, new Color(48, 48, 52));
        if (!_demo)
        {
            // В обучении на этом месте кнопки "Назад"/"Далее".
            g.Text(_level.Number == 0 ? "Программа" : $"Программа: {_level.Number}", new Vector2(Gfx.Width / 2f, bar.Y + 10), 52, Color.White, Align.Center);
        }
        var stats = _editor.ToProgram().Stats;
        g.Text($"команд: {stats.Actions}", new Vector2(Gfx.Width - 40, bar.Y + 20), 38, new Color(200, 200, 200), Align.Right);

        if (_countPicker is not null)
        {
            DrawCountPicker(g);
        }
        if (_toastLeft > 0 && _toast is not null)
        {
            var panel = RectangleF.FromCenter(new Vector2(ProgramArea.Center.X, ProgramArea.Bottom - 60), 1000, 100);
            g.Panel(panel, new Color(255, 248, 225, 245), new Color(60, 50, 40), 20, 3);
            g.TextBlock(_toast, panel.Inflate(-16), 38, new Color(120, 40, 30), bold: true);
        }
        _dialog?.Draw(g);
    }

    private void OnChanged()
    {
        _layout = ProgramLayout.Build(_editor);
        ScrollToCursor(immediate: false);
        if (!_demo)
        {
            Game.Session.SaveProgram(_level.Number, _editor.ToProgram());
        }
    }

    private void AddCommand(Command command)
    {
        if (!_editor.InsertCommand(command))
        {
            Toast("Программа слишком длинная");
        }
    }

    private void AddIf()
    {
        if (!_editor.CanInsertIf)
        {
            Toast(_editor.BlockCount + 2 > ProgramEditor.MaxBlocks ? "Программа слишком длинная" : "Условие нельзя поставить внутрь другого условия");
            return;
        }
        _editor.InsertIf();
    }

    private void AddLoop()
    {
        if (!_editor.CanInsertLoop)
        {
            Toast(_editor.BlockCount + 2 > ProgramEditor.MaxBlocks ? "Программа слишком длинная" : "Цикл нельзя поставить внутрь другого цикла");
            return;
        }
        _editor.InsertLoop();
        _countPicker = (EditLoop)_layout.Blocks.First(b => b.Slot.Equals(_editor.Cursor)).Node!;
    }

    private void Backspace()
    {
        if (!_editor.Backspace() && _editor.Cursor.Index == 0 && !ReferenceEquals(_editor.Cursor.Sequence, _editor.Root))
        {
            Toast("Сначала удали команды внутри");
        }
    }

    private void SetLoopCount(int count)
    {
        if (_countPicker is not null)
        {
            _editor.SetLoopCount(_countPicker, count);
        }
        _countPicker = null;
    }

    private void AskClear()
    {
        if (_editor.IsEmpty)
        {
            return;
        }
        if (_demo)
        {
            _editor.Clear();
            return;
        }
        _dialog = new Dialog("Удалить все команды?", ("Удалить", _editor.Clear), ("Отмена", () => { }));
    }

    private void Toast(string text)
    {
        _toast = text;
        _toastLeft = 2.5f;
    }

    private void GoBack()
    {
        if (!_demo)
        {
            Game.Show(new LevelScreen(Game, _level));
        }
    }

    private RectangleF BlockRect(LayoutBlock block) =>
        new(ProgramArea.X + 30 + block.Column * PitchX - _scroll, ProgramArea.Y + 40 + block.Row * PitchY, CellWidth, CellHeight);

    private Vector2 CellCenter(int column, int row) =>
        new(ProgramArea.X + 30 + column * PitchX - _scroll + CellWidth / 2, ProgramArea.Y + 40 + row * PitchY + CellHeight * 0.55f);

    private void ScrollToCursor(bool immediate)
    {
        var block = _layout.CursorBlock(_editor);
        var left = 30 + block.Column * PitchX;
        var visible = ProgramArea.Width - 60;
        if (left - _targetScroll > visible - PitchX * 1.5f)
        {
            _targetScroll = MathF.Ceiling((left - visible + PitchX * 1.5f) / PitchX) * PitchX;
        }
        else if (left - _targetScroll < PitchX)
        {
            _targetScroll = Math.Max(0, MathF.Floor((left - PitchX) / PitchX) * PitchX);
        }
        ClampScroll();
        if (immediate)
        {
            _scroll = _targetScroll;
        }
    }

    private void ClampScroll()
    {
        // Кратно колонке, чтобы при прокрутке к курсору крайний левый блок не обрезался.
        var max = Math.Max(0, MathF.Ceiling((60 + _layout.Columns * PitchX + PitchX - ProgramArea.Width) / PitchX) * PitchX);
        _targetScroll = Math.Clamp(_targetScroll, 0, max);
    }

    private void UpdateProgramArea(Input input)
    {
        if (input.Wheel != 0 && ProgramArea.Contains(input.Pointer))
        {
            _targetScroll -= input.Wheel * 0.8f;
            ClampScroll();
        }

        if (input.PointerPressed && ProgramArea.Contains(input.Pointer))
        {
            _pressStart = input.Pointer;
            _pressScroll = _targetScroll;
            _dragging = false;
        }
        if (_pressStart is { } start && input.PointerDown)
        {
            var dx = input.Pointer.X - start.X;
            if (Math.Abs(dx) > DragThreshold)
            {
                _dragging = true;
            }
            if (_dragging)
            {
                _targetScroll = _pressScroll - dx;
                ClampScroll();
                _scroll = _targetScroll;
            }
        }
        if (_pressStart is not null && input.PointerReleased)
        {
            if (!_dragging)
            {
                ClickBlock(input.Pointer);
            }
            _pressStart = null;
            _dragging = false;
        }
    }

    private void ClickBlock(Vector2 point)
    {
        var block = _layout.Blocks.FirstOrDefault(b => BlockRect(b).Inflate(8).Contains(point));
        if (block is null)
        {
            return;
        }
        if (block.Kind == BlockKind.LoopStart && block.Slot.Equals(_editor.Cursor))
        {
            _countPicker = (EditLoop)block.Node!;
            return;
        }
        _editor.SetCursor(block.Slot);
    }

    private void DrawProgram(Gfx g)
    {
        var device = g.Batch.GraphicsDevice;
        g.End();
        var previous = device.ScissorRectangle;
        device.ScissorRectangle = ProgramArea.ToRectangle();
        g.Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, rasterizerState: Scissor);

        foreach (var link in _layout.Links)
        {
            g.Line(CellCenter(link.FromColumn, link.FromRow), CellCenter(link.ToColumn, link.ToRow), 6, LinkColor);
        }

        var cursor = _layout.CursorBlock(_editor);
        foreach (var block in _layout.Blocks)
        {
            var rect = BlockRect(block);
            if (rect.Right < ProgramArea.X || rect.X > ProgramArea.Right)
            {
                continue;
            }
            if (block == cursor)
            {
                g.RoundRect(rect.Inflate(10), 18, new Color(255, 225, 60));
            }
            g.SpriteFit(SpriteFor(block.Kind), rect);
            switch (block.Kind)
            {
                case BlockKind.IfStart:
                    g.Text("Да", new Vector2(rect.Right + (PitchX - CellWidth) / 2, rect.Y - 44), 34, new Color(20, 90, 20), Align.Center, bold: true);
                    break;
                case BlockKind.IfElse:
                    g.TextBlock("Нет", rect.Offset(new Vector2(0, 14)), 36, new Color(150, 30, 30), bold: true);
                    break;
                case BlockKind.LoopStart:
                    var badge = new Vector2(rect.Right - 14, rect.Y + 22);
                    g.Circle(badge, 30, new Color(40, 40, 40));
                    g.Circle(badge, 26, Color.White);
                    g.Text(((EditLoop)block.Node!).Count.ToString(), badge - new Vector2(0, 21), 36, new Color(30, 30, 30), Align.Center, bold: true);
                    break;
            }
        }
        var caretX = BlockRect(cursor).Right + (PitchX - CellWidth) / 2;
        var caretY = BlockRect(cursor).Y;
        g.Rect(new RectangleF(caretX - 3, caretY - 6, 6, CellHeight + 12), new Color(255, 200, 0));

        g.End();
        device.ScissorRectangle = previous;
        g.Begin();
    }

    private static readonly RasterizerState Scissor = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    private static string SpriteFor(BlockKind kind) => kind switch
    {
        BlockKind.LoopStart => "program_sprite_0",
        BlockKind.LoopEnd => "program_sprite_1",
        BlockKind.IfStart => "program_sprite_2",
        BlockKind.IfEnd => "program_sprite_3",
        BlockKind.Forward => "program_sprite_4",
        BlockKind.TurnLeft => "program_sprite_5",
        BlockKind.IfElse => "program_sprite_6",
        BlockKind.Back => "program_sprite_7",
        BlockKind.TurnRight => "program_sprite_8",
        BlockKind.Start => "program_sprite_9",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Мини-карта начального состояния уровня: столбец - X клетки, строка - Y.</summary>
    private void DrawMinimap(Gfx g)
    {
        var area = MinimapArea;
        var n = LevelDefinition.InnerSize;
        var cell = area.Width / n;
        g.Rect(area.Inflate(4), new Color(30, 20, 15));
        g.Rect(area, new Color(120, 72, 45));
        for (var i = 1; i < n; i++)
        {
            g.Rect(new RectangleF(area.X + i * cell - 2, area.Y, 4, area.Height), new Color(30, 20, 15));
            g.Rect(new RectangleF(area.X, area.Y + i * cell - 2, area.Width, 4), new Color(30, 20, 15));
        }

        RectangleF Cell(GridPos p) => new(area.X + (p.X - 1) * cell, area.Y + (p.Y - 1) * cell, cell, cell);
        void Icon(string sprite, GridPos p, float scale = 0.7f) => g.SpriteFit(sprite, Cell(p).Scale(scale));

        foreach (var p in _level.Trash)
        {
            Icon("trash", p);
        }
        foreach (var p in _level.Soil)
        {
            Icon("soil", p);
        }
        foreach (var p in _level.Quests)
        {
            Icon("quest", p);
        }
        foreach (var p in _level.Cubes)
        {
            Icon("plant", p);
        }
        Icon(_level.StartHeading.Name(), _level.Robot, 0.8f);
    }

    private void UpdateCountPicker(Input input)
    {
        var panel = CountPickerPanel;
        for (var i = 0; i < _countButtons.Count; i++)
        {
            var b = _countButtons[i];
            b.Bounds = new RectangleF(panel.X + 30 + (i % 4) * 130, panel.Y + 110 + (i / 4) * 130, 110, 110);
            b.Selected = _countPicker!.Count == i + ProgramEditor.MinLoopCount;
            b.Update(input);
        }
        if (input.PointerPressed && !panel.Contains(input.Pointer))
        {
            _countPicker = null;
        }
        input.ConsumePointer();
    }

    private static RectangleF CountPickerPanel => RectangleF.FromCenter(new Vector2(ProgramArea.Center.X, 480), 580, 400);

    private void DrawCountPicker(Gfx g)
    {
        g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Color.Black * 0.35f);
        var panel = CountPickerPanel;
        g.Panel(panel, new Color(255, 248, 225), new Color(60, 50, 40), 26, 4);
        g.Text("Сколько раз повторить?", new Vector2(panel.Center.X, panel.Y + 30), 44, new Color(40, 35, 30), Align.Center, bold: true);
        foreach (var b in _countButtons)
        {
            b.Draw(g);
        }
    }
}
