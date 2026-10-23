using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Editing;

public enum BlockKind
{
    Start,
    Forward,
    Back,
    TurnLeft,
    TurnRight,

    /// <summary>Проверка "впереди радиация?". Своей позиции курсора у неё нет.</summary>
    IfStart,

    /// <summary>Метка "Да" справа от проверки: начало ветки "Да".</summary>
    IfThen,

    /// <summary>Метка "Нет" под меткой "Да": начало ветки "Нет".</summary>
    IfElse,

    /// <summary>Слияние веток; через него курсор ставится после условия.</summary>
    IfEnd,

    /// <summary>Начало цикла (с числом повторов); через него курсор ставится в начало тела.</summary>
    LoopStart,
    LoopEnd,
}

/// <summary>
/// Блок на экране редактора. Нажатие на блок ставит курсор в <see cref="Slot"/> - новые блоки
/// вставляются сразу после него.
/// </summary>
public sealed record LayoutBlock(BlockKind Kind, int Column, int Row, EditorSlot? Slot, EditNode? Node)
{
    public bool Holds(EditorSlot slot) => Slot is { } own && own.Equals(slot);
}

/// <summary>Соединительная линия между центрами клеток сетки.</summary>
public readonly record struct LayoutLink(int FromColumn, int FromRow, int ToColumn, int ToRow);

/// <summary>
/// Раскладка программы по сетке: основная строка слева направо. Условие расходится на две строки:
/// ветка "Да" продолжает строку, ветка "Нет" идёт ниже, а в конце они сходятся в блоке слияния.
/// </summary>
public sealed class ProgramLayout
{
    private readonly List<LayoutBlock> _blocks = [];
    private readonly List<LayoutLink> _links = [];

    private ProgramLayout()
    {
    }

    public IReadOnlyList<LayoutBlock> Blocks => _blocks;
    public IReadOnlyList<LayoutLink> Links => _links;
    public int Columns { get; private set; }
    public int Rows { get; private set; }

    public static ProgramLayout Build(ProgramEditor editor)
    {
        var layout = new ProgramLayout();
        var root = (List<EditNode>)editor.Root;
        layout.Add(BlockKind.Start, 0, 0, new EditorSlot(root, 0), null);
        var (end, height) = layout.Place(root, 1, 0, 0);
        layout.Columns = end;
        layout.Rows = height;
        return layout;
    }

    public LayoutBlock CursorBlock(ProgramEditor editor) => _blocks.First(b => b.Holds(editor.Cursor));

    /// <summary>Раскладывает список, начиная с колонки <paramref name="column"/>; возвращает следующую свободную колонку и высоту в строках.</summary>
    private (int End, int Height) Place(List<EditNode> seq, int column, int row, int linkFrom)
    {
        var height = 1;
        var prev = linkFrom;
        for (var i = 0; i < seq.Count; i++)
        {
            var after = new EditorSlot(seq, i + 1);
            Link(prev, row, column, row);
            switch (seq[i])
            {
                case EditCommand c:
                    Add(Kind(c.Command), column, row, after, c);
                    prev = column;
                    column++;
                    break;

                case EditLoop loop:
                {
                    Add(BlockKind.LoopStart, column, row, new EditorSlot(loop.Body, 0), loop);
                    var (end, h) = Place(loop.Body, column + 1, row, column);
                    Link(end - 1, row, end, row);
                    Add(BlockKind.LoopEnd, end, row, after, loop);
                    height = Math.Max(height, h);
                    prev = end;
                    column = end + 1;
                    break;
                }

                case EditIf iff:
                {
                    var fork = column;
                    Add(BlockKind.IfStart, fork, row, null, iff);
                    Add(BlockKind.IfThen, fork + 1, row, new EditorSlot(iff.Then, 0), iff);
                    Link(fork, row, fork + 1, row);
                    var (thenEnd, thenHeight) = Place(iff.Then, fork + 2, row, fork + 1);

                    var elseRow = row + thenHeight;
                    Add(BlockKind.IfElse, fork + 1, elseRow, new EditorSlot(iff.Else, 0), iff);
                    _links.Add(new LayoutLink(fork, row, fork, elseRow));
                    _links.Add(new LayoutLink(fork, elseRow, fork + 1, elseRow));
                    var (elseEnd, elseHeight) = Place(iff.Else, fork + 2, elseRow, fork + 1);

                    var end = Math.Max(thenEnd, elseEnd);
                    Link(thenEnd - 1, row, end, row);
                    _links.Add(new LayoutLink(elseEnd - 1, elseRow, end, elseRow));
                    _links.Add(new LayoutLink(end, elseRow, end, row));
                    Add(BlockKind.IfEnd, end, row, after, iff);
                    height = Math.Max(height, thenHeight + elseHeight);
                    prev = end;
                    column = end + 1;
                    break;
                }
            }
        }
        return (column, height);
    }

    private void Link(int fromColumn, int fromRow, int toColumn, int toRow)
    {
        if (fromColumn != toColumn)
        {
            _links.Add(new LayoutLink(fromColumn, fromRow, toColumn, toRow));
        }
    }

    private void Add(BlockKind kind, int column, int row, EditorSlot? slot, EditNode? node) =>
        _blocks.Add(new LayoutBlock(kind, column, row, slot, node));

    private static BlockKind Kind(Command command) => command switch
    {
        Command.Forward => BlockKind.Forward,
        Command.Back => BlockKind.Back,
        Command.TurnLeft => BlockKind.TurnLeft,
        Command.TurnRight => BlockKind.TurnRight,
        _ => throw new NotSupportedException(command.ToString()),
    };
}
