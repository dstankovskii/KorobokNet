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
/// снизу - палитра команд. Ленту листают стрелками по бокам, колесом, тачпадом или перетаскиванием,
/// блок выделяется нажатием, а удаляется крестиком, который появляется при наведении.
/// Программа сохраняется при каждом изменении.
/// </summary>
public sealed class ProgramScreen : Screen
{
    private const float CellWidth = 118;
    private const float CellHeight = 148;
    private const float PitchX = 140;
    private const float PitchY = 200;
    private const float DragThreshold = 12;
    private const float DeleteRadius = 22;
    private const int ArrowStep = 4;
    private const float Margin = 25;
    // В обучении внизу экрана кнопки "Назад"/"Далее", под них оставляем полосу.
    private const float StoryNavHeight = 100;

    private static readonly Color Background = new(96, 178, 226);
    private static readonly Color PanelColor = new(140, 200, 238);
    private static readonly Color LinkColor = new(40, 40, 40);
    private static readonly RectangleF MinimapArea = new(1450, 50, 430, 430);

    // Панель ленты; по бокам на ней стрелки, между ними сами блоки.
    private readonly RectangleF _strip;
    private readonly RectangleF _programArea;
    private readonly RectangleF _paletteArea;
    private readonly LevelDefinition _level;
    private readonly bool _demo;
    private readonly ProgramEditor _editor;
    private readonly Dictionary<string, Button> _palette = [];
    private readonly Button _done;
    private readonly Button _clear;
    private readonly Button _scrollLeft;
    private readonly Button _scrollRight;
    private readonly List<Button> _countButtons = [];
    private ProgramLayout _layout;
    private float _scroll;
    private float _targetScroll;
    private float _rowTop;
    private Vector2? _pressStart;
    private float _pressScroll;
    private bool _dragging;
    private bool _keepScroll;
    private LayoutBlock? _hover;
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

        var bottom = Gfx.Height - Margin - (demo ? StoryNavHeight : 0);
        _paletteArea = new RectangleF(20, bottom - 215, 1010, 215);
        _strip = new RectangleF(20, 40, 1390, _paletteArea.Y - Margin - 40);
        _programArea = new RectangleF(_strip.X + 100, _strip.Y + 8, _strip.Width - 200, _strip.Height - 16);

        _done = Button.Primary(new RectangleF(1580, bottom - 100, 300, 100), "Готово", GoBack);

        (string Key, BlockKind Kind, string Label, Action Action)[] palette =
        [
            ("forward", BlockKind.Forward, "Вперёд", () => AddCommand(Command.Forward)),
            ("left", BlockKind.TurnLeft, "Влево", () => AddCommand(Command.TurnLeft)),
            ("right", BlockKind.TurnRight, "Вправо", () => AddCommand(Command.TurnRight)),
            ("back", BlockKind.Back, "Назад", () => AddCommand(Command.Back)),
            ("if", BlockKind.IfStart, "Условие", AddIf),
            ("loop", BlockKind.LoopStart, "Цикл", AddLoop),
        ];
        for (var i = 0; i < palette.Length; i++)
        {
            var (key, kind, label, action) = palette[i];
            var (accent, icon) = Look(kind);
            var rect = new RectangleF(_paletteArea.X + 25 + i * 165, _paletteArea.Y + 20, 140, 175);
            _palette[key] = new Button(rect, onClick: action) { Painter = (g, r, tint) => Blocks.Draw(g, r, accent, icon, tint, label) };
        }

        _clear = new Button(new RectangleF(1250, bottom - 100, 300, 100), text: "Очистить", onClick: AskClear)
        {
            TextSize = 42,
            TextColor = new Color(180, 50, 40),
        };

        _scrollLeft = new Button(RectangleF.FromCenter(new Vector2(_strip.X + 50, _strip.Center.Y), 76, 76), onClick: () => ScrollBy(-1))
        {
            Painter = Icons.RoundButton(Icons.BlueButton, "chevron_left"),
        };
        _scrollRight = new Button(RectangleF.FromCenter(new Vector2(_strip.Right - 50, _strip.Center.Y), 76, 76), onClick: () => ScrollBy(1))
        {
            Painter = Icons.RoundButton(Icons.BlueButton, "chevron_right"),
        };

        for (var n = ProgramEditor.MinLoopCount; n <= ProgramEditor.MaxLoopCount; n++)
        {
            var count = n;
            _countButtons.Add(new Button(default, text: n.ToString(), onClick: () => SetLoopCount(count)) { TextSize = 50 });
        }
        ScrollToCursor(immediate: true);
        _rowTop = RowTop;
    }

    public ProgramEditor Editor => _editor;

    public override RectangleF? Anchor(string name) => name switch
    {
        "program" => _layout.Blocks.Select(BlockRect).Aggregate(RectangleF.Union),
        "minimap" => MinimapArea,
        "palette" => _paletteArea,
        "clear" => _clear.Bounds,
        "last-block" => LastBlock() is { } last ? BlockRect(last) : null,
        "last-block.delete" => LastBlock() is { } last ? RectangleF.FromCenter(DeleteCenter(BlockRect(last)), DeleteRadius * 2, DeleteRadius * 2) : null,
        "back" or "done" => _done.Bounds,
        _ when name.StartsWith("palette.", StringComparison.Ordinal) && _palette.TryGetValue(name["palette.".Length..], out var b) => b.Bounds,
        _ => null,
    };

    public override void Update(float dt, Input input)
    {
        _toastLeft -= dt;
        _scroll = MathHelper.Lerp(_scroll, _targetScroll, Math.Min(1, dt * 12));
        _rowTop = MathHelper.Lerp(_rowTop, RowTop, Math.Min(1, dt * 12));
        _hover = null;

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
        _clear.Update(input);
        _scrollLeft.Enabled = _targetScroll > 0;
        _scrollRight.Enabled = _targetScroll < MaxScroll;
        _scrollLeft.Update(input);
        _scrollRight.Update(input);
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
        DrawBackground(g);
        _done.Draw(g);

        g.RoundRect(_strip, 24, PanelColor);
        DrawProgram(g);
        _scrollLeft.Draw(g);
        _scrollRight.Draw(g);
        DrawMinimap(g);

        g.RoundRect(_paletteArea, 16, PanelColor);
        foreach (var (key, button) in _palette)
        {
            button.Draw(g);
            // Недоступную команду всё равно можно нажать, тогда покажем, почему нельзя. Рисуем её бледнее.
            if ((key == "if" && !_editor.CanInsertIf) || (key == "loop" && !_editor.CanInsertLoop))
            {
                g.RoundRect(button.Bounds, 12, PanelColor * 0.65f);
            }
        }
        _clear.Draw(g);

        if (_countPicker is not null)
        {
            DrawCountPicker(g);
        }
        if (_toastLeft > 0 && _toast is not null)
        {
            var panel = RectangleF.FromCenter(new Vector2(_programArea.Center.X, _programArea.Bottom - 60), 1000, 100);
            g.Panel(panel, new Color(255, 248, 225, 245), new Color(60, 50, 40), 20, 3);
            g.TextBlock(_toast, panel.Inflate(-16), 38, new Color(120, 40, 30), bold: true);
        }
        _dialog?.Draw(g);
    }

    /// <summary>Однотонный фон с едва заметными рисунками: стрелки, звёзды, ящики, листья.</summary>
    private static void DrawBackground(Gfx g)
    {
        const int tile = 400;
        g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Background);
        for (var y = 0; y < Gfx.Height; y += tile)
        {
            for (var x = 0; x < Gfx.Width; x += tile)
            {
                Icons.Draw(g, "wallpaper", new RectangleF(x, y, tile, tile), Color.White * 0.13f);
            }
        }
    }

    private void OnChanged()
    {
        _layout = ProgramLayout.Build(_editor);
        if (_keepScroll)
        {
            ClampScroll();
        }
        else
        {
            ScrollToCursor(immediate: false);
        }
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
        _countPicker = (EditLoop)_layout.Blocks.First(b => b.Holds(_editor.Cursor)).Node!;
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
        new(_programArea.X + 30 + block.Column * PitchX - _scroll, _rowTop + block.Row * PitchY, CellWidth, CellHeight);

    private Vector2 CellCenter(int column, int row) =>
        new(_programArea.X + 30 + column * PitchX - _scroll + CellWidth / 2, _rowTop + row * PitchY + CellHeight * 0.55f);

    // Верх первой строки: блоки стоят посередине панели, и с развилкой тоже.
    private float RowTop => _programArea.Center.Y - ((_layout.Rows - 1) * PitchY + CellHeight) / 2;

    private void ScrollBy(int direction)
    {
        _targetScroll += direction * ArrowStep * PitchX;
        ClampScroll();
    }

    private void ScrollToCursor(bool immediate)
    {
        var block = _layout.CursorBlock(_editor);
        var left = 30 + block.Column * PitchX;
        var visible = _programArea.Width - 60;
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

    // Кратно колонке, чтобы при прокрутке к курсору крайний левый блок не обрезался.
    private float MaxScroll => Math.Max(0, MathF.Ceiling((60 + _layout.Columns * PitchX + PitchX - _programArea.Width) / PitchX) * PitchX);

    private void ClampScroll() => _targetScroll = Math.Clamp(_targetScroll, 0, MaxScroll);

    private void UpdateProgramArea(Input input)
    {
        if ((input.Wheel != 0 || input.WheelX != 0) && _strip.Contains(input.Pointer))
        {
            _targetScroll += (input.WheelX - input.Wheel) * 0.8f;
            ClampScroll();
        }

        if (!_dragging && _programArea.Contains(input.Pointer))
        {
            _hover = _layout.Blocks.FirstOrDefault(b => CanDelete(b.Kind) && BlockRect(b).Inflate(16).Contains(input.Pointer));
        }

        if (input.PointerPressed && _programArea.Contains(input.Pointer))
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
                if (_hover is not null && Vector2.Distance(input.Pointer, DeleteCenter(BlockRect(_hover))) <= DeleteRadius + 4)
                {
                    RemoveBlock(_hover);
                }
                else
                {
                    ClickBlock(input.Pointer);
                }
            }
            _pressStart = null;
            _dragging = false;
        }
    }

    private static bool CanDelete(BlockKind kind) =>
        kind is BlockKind.Forward or BlockKind.Back or BlockKind.TurnLeft or BlockKind.TurnRight or BlockKind.IfStart or BlockKind.LoopStart;

    /// <summary>Крестик удаления в правом верхнем углу карточки.</summary>
    private static Vector2 DeleteCenter(RectangleF block) => new(block.Right - 14, block.Y + 6);

    private LayoutBlock? LastBlock() => _layout.Blocks.LastOrDefault(b => CanDelete(b.Kind));

    private void RemoveBlock(LayoutBlock block)
    {
        // Удаляют обычно то, что видно, поэтому ленту к курсору не прокручиваем.
        _keepScroll = true;
        _editor.Remove(block.Node!);
        _keepScroll = false;
        _hover = null;
    }

    private void ClickBlock(Vector2 point)
    {
        var block = _layout.Blocks.FirstOrDefault(b => BlockRect(b).Inflate(8).Contains(point));
        if (block is null)
        {
            return;
        }
        if (block.Kind == BlockKind.LoopStart && block.Holds(_editor.Cursor))
        {
            _countPicker = (EditLoop)block.Node!;
            return;
        }
        // Нажатие на саму проверку ставит курсор в ветку "Да".
        _editor.SetCursor(block.Slot ?? new EditorSlot(((EditIf)block.Node!).Then, 0));
    }

    private void DrawProgram(Gfx g)
    {
        var device = g.Batch.GraphicsDevice;
        g.End();
        var previous = device.ScissorRectangle;
        device.ScissorRectangle = _programArea.ToRectangle();
        g.Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, rasterizerState: Scissor);

        foreach (var link in _layout.Links)
        {
            g.Line(CellCenter(link.FromColumn, link.FromRow), CellCenter(link.ToColumn, link.ToRow), 6, LinkColor);
        }

        var cursor = _layout.CursorBlock(_editor);
        foreach (var block in _layout.Blocks)
        {
            var rect = BlockRect(block);
            if (rect.Right < _programArea.X || rect.X > _programArea.Right)
            {
                continue;
            }
            if (block == cursor)
            {
                // Новые блоки встают сразу после выделенного; подсвечиваем его мягким белым ореолом.
                g.RoundRect(rect.Inflate(12), 22, Color.White * 0.35f);
                g.RoundRect(rect.Inflate(6), 18, Color.White * 0.8f);
            }
            var linkY = CellCenter(block.Column, block.Row).Y;
            g.Circle(new Vector2(rect.X + 4, linkY), 8, LinkColor);
            g.Circle(new Vector2(rect.Right - 4, linkY), 8, LinkColor);
            var (accent, icon) = Look(block.Kind);
            Blocks.Draw(g, new RectangleF(rect.X + 8, rect.Y, rect.Width - 16, rect.Height), accent, icon, label: Label(block.Kind));
            switch (block.Kind)
            {
                case BlockKind.IfThen:
                    g.TextBlock("Да", BlockFace(rect), 46, YesColor, bold: true);
                    break;
                case BlockKind.IfElse:
                    g.TextBlock("Нет", BlockFace(rect), 46, NoColor, bold: true);
                    break;
                case BlockKind.LoopStart:
                    // Число повторов внизу: правый верхний угол занимает крестик удаления.
                    var badge = new Vector2(rect.Right - 14, rect.Bottom - 22);
                    g.Circle(badge, 30, new Color(40, 40, 40));
                    g.Circle(badge, 26, Color.White);
                    g.Text(((EditLoop)block.Node!).Count.ToString(), badge - new Vector2(0, 21), 36, new Color(30, 30, 30), Align.Center, bold: true);
                    break;
            }
        }
        if (_hover is not null)
        {
            var center = DeleteCenter(BlockRect(_hover));
            Icons.RoundButton(Icons.RedButton, "cross")(g, RectangleF.FromCenter(center, DeleteRadius * 2, DeleteRadius * 2), Color.White);
        }

        g.End();
        device.ScissorRectangle = previous;
        g.Begin();
    }

    private static readonly RasterizerState Scissor = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    private static readonly Color YesColor = new(34, 140, 50);
    private static readonly Color NoColor = new(200, 50, 40);

    /// <summary>Белая часть карточки под шапкой.</summary>
    private static RectangleF BlockFace(RectangleF rect)
    {
        var header = rect.Height * Blocks.HeaderShare(string.Empty);
        return new(rect.X + 8, rect.Y + header, rect.Width - 16, rect.Height - header);
    }

    // Условие в шапке звучит как вопрос, ветки ниже отвечают "Да" и "Нет".
    private static string Label(BlockKind kind) => kind switch
    {
        BlockKind.Forward => "Вперёд",
        BlockKind.Back => "Назад",
        BlockKind.TurnLeft => "Влево",
        BlockKind.TurnRight => "Вправо",
        BlockKind.LoopStart => "Цикл",
        BlockKind.IfStart => "Радиация?",
        BlockKind.Start => "Старт",
        _ => string.Empty,
    };

    private static (Color Accent, string? Icon) Look(BlockKind kind) => kind switch
    {
        BlockKind.Forward => (Blocks.Action, "forward"),
        BlockKind.Back => (Blocks.Action, "back"),
        BlockKind.TurnLeft => (Blocks.Action, "turn_left"),
        BlockKind.TurnRight => (Blocks.Action, "turn_right"),
        BlockKind.LoopStart => (Blocks.Control, "loop"),
        BlockKind.LoopEnd => (Blocks.Control, "loop_end"),
        BlockKind.IfStart => (Blocks.Control, "if"),
        BlockKind.IfThen or BlockKind.IfElse => (Blocks.Control, null),
        BlockKind.IfEnd => (Blocks.Control, "if_end"),
        BlockKind.Start => (Blocks.Control, "start"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>Мини-карта начального состояния уровня: столбец - X клетки, строка - Y.</summary>
    private void DrawMinimap(Gfx g)
    {
        const float gap = 5;
        var area = MinimapArea;
        var n = LevelDefinition.InnerSize;
        var cell = (area.Width - gap) / n;
        var frame = area.Inflate(10);
        g.RoundRect(frame.Offset(new Vector2(0, 8)), 26, Color.Black * 0.18f);
        g.Panel(frame, new Color(132, 98, 62), new Color(86, 62, 38), 26, 5);

        RectangleF Cell(GridPos p) =>
            new(area.X + gap + (p.X - 1) * cell, area.Y + gap + (p.Y - 1) * cell, cell - gap, cell - gap);

        for (var y = 1; y <= n; y++)
        {
            for (var x = 1; x <= n; x++)
            {
                var ground = (x + y) % 2 == 0 ? new Color(228, 206, 156) : new Color(216, 192, 140);
                g.RoundRect(Cell(new GridPos(x, y)), 10, ground);
            }
        }

        foreach (var p in _level.Soil)
        {
            Icons.Draw(g, "soil", Cell(p));
        }
        foreach (var p in _level.Trash)
        {
            Icons.Draw(g, "stones", Cell(p).Scale(0.9f));
        }
        foreach (var p in _level.Quests)
        {
            Icons.Draw(g, "quest", Cell(p).Scale(0.72f));
        }
        foreach (var p in _level.Cubes)
        {
            Icons.Draw(g, "crate", Cell(p).Scale(0.86f));
        }
        // Куда робот едет, показывает стрелка у края клетки.
        var ahead = _level.StartHeading.Offset();
        var dir = new Vector2(ahead.X, ahead.Y);
        var robot = Cell(_level.Robot);
        var pointer = robot.Center + dir * robot.Width * 0.56f;
        Icons.Draw(g, "pointer", RectangleF.FromCenter(pointer, robot.Width * 0.5f, robot.Width * 0.5f), rotation: MathF.Atan2(dir.Y, dir.X));
        Icons.Draw(g, "robot", robot.Scale(0.6f));
    }

    private void UpdateCountPicker(Input input)
    {
        var panel = CountPickerPanel;
        for (var i = 0; i < _countButtons.Count; i++)
        {
            var b = _countButtons[i];
            b.Bounds = new RectangleF(panel.X + 30 + (i % 4) * 130, panel.Y + 110 + (i / 4) * 130, 110, 110);
            b.Selected = _countPicker!.Count == i + ProgramEditor.MinLoopCount;
            if (b.Update(input))
            {
                // Нажатие закрыло окно, остальные кнопки уже не нужны.
                return;
            }
        }
        if (input.PointerPressed && !panel.Contains(input.Pointer))
        {
            _countPicker = null;
        }
        input.ConsumePointer();
    }

    private RectangleF CountPickerPanel => RectangleF.FromCenter(new Vector2(_programArea.Center.X, 480), 580, 400);

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
