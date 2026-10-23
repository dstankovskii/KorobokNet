using System.Runtime.CompilerServices;
using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Editing;

/// <summary>Узел редактируемой программы (изменяемый, в отличие от <see cref="Instruction"/>).</summary>
public abstract class EditNode;

public sealed class EditCommand(Command command) : EditNode
{
    public Command Command { get; } = command;
}

/// <summary>"Если впереди радиация": ветка "Да" и ветка "Нет".</summary>
public sealed class EditIf : EditNode
{
    public List<EditNode> Then { get; } = [];
    public List<EditNode> Else { get; } = [];
}

public sealed class EditLoop(int count) : EditNode
{
    public int Count { get; set; } = count;
    public List<EditNode> Body { get; } = [];
}

/// <summary>Место вставки: позиция <see cref="Index"/> внутри списка <see cref="Sequence"/>.</summary>
public readonly record struct EditorSlot(List<EditNode> Sequence, int Index)
{
    public bool Equals(EditorSlot other) => ReferenceEquals(Sequence, other.Sequence) && Index == other.Index;

    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Sequence), Index);
}

/// <summary>
/// Редактор программы робота: вставка блоков в позицию курсора, удаление, перемещение курсора.
/// Правила как в оригинале: условие нельзя вложить в условие, цикл - в цикл.
/// </summary>
public sealed class ProgramEditor
{
    public const int MinLoopCount = 2;
    public const int MaxLoopCount = 9;
    public const int DefaultLoopCount = 2;

    /// <summary>Ограничение, чтобы программа помещалась на экране и оставалась обозримой.</summary>
    public const int MaxBlocks = 60;

    private readonly List<EditNode> _root = [];

    public ProgramEditor()
    {
        Cursor = new EditorSlot(_root, 0);
    }

    public IReadOnlyList<EditNode> Root => _root;

    public EditorSlot Cursor { get; private set; }

    /// <summary>Вызывается после любого изменения программы или курсора.</summary>
    public event Action? Changed;

    public int BlockCount => Count(_root);

    public bool IsEmpty => _root.Count == 0;

    public static ProgramEditor FromProgram(RobotProgram program)
    {
        var editor = new ProgramEditor();
        Fill(editor._root, program.Instructions);
        editor.Cursor = new EditorSlot(editor._root, editor._root.Count);
        return editor;
    }

    public RobotProgram ToProgram() => new(Convert(_root));

    public bool CanInsertIf => !IsInside<EditIf>(Cursor.Sequence) && BlockCount + 2 <= MaxBlocks;

    public bool CanInsertLoop => !IsInside<EditLoop>(Cursor.Sequence) && BlockCount + 2 <= MaxBlocks;

    public bool CanInsertCommand => BlockCount + 1 <= MaxBlocks;

    public bool InsertCommand(Command command)
    {
        if (!CanInsertCommand)
        {
            return false;
        }
        Insert(new EditCommand(command));
        return true;
    }

    /// <summary>Вставляет условие; курсор переходит в начало ветки "Да".</summary>
    public bool InsertIf()
    {
        if (!CanInsertIf)
        {
            return false;
        }
        var node = new EditIf();
        Insert(node);
        Cursor = new EditorSlot(node.Then, 0);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Вставляет цикл; курсор переходит в начало тела цикла.</summary>
    public bool InsertLoop(int count = DefaultLoopCount)
    {
        if (!CanInsertLoop)
        {
            return false;
        }
        var node = new EditLoop(Math.Clamp(count, MinLoopCount, MaxLoopCount));
        Insert(node);
        Cursor = new EditorSlot(node.Body, 0);
        Changed?.Invoke();
        return true;
    }

    public void SetLoopCount(EditLoop loop, int count)
    {
        loop.Count = Math.Clamp(count, MinLoopCount, MaxLoopCount);
        Changed?.Invoke();
    }

    /// <summary>
    /// Удаляет блок перед курсором (условие или цикл - целиком, с содержимым).
    /// В начале пустой ветки или пустого цикла удаляет сам пустой блок.
    /// </summary>
    public bool Backspace()
    {
        var (seq, index) = Cursor;
        if (index > 0)
        {
            seq.RemoveAt(index - 1);
            Cursor = new EditorSlot(seq, index - 1);
            Changed?.Invoke();
            return true;
        }

        if (FindOwner(_root, seq) is not { } owner)
        {
            return false;
        }
        var empty = owner.Node switch
        {
            EditIf i => i.Then.Count == 0 && i.Else.Count == 0,
            EditLoop l => l.Body.Count == 0,
            _ => false,
        };
        if (!empty)
        {
            return false;
        }
        owner.ParentSequence.RemoveAt(owner.Index);
        Cursor = new EditorSlot(owner.ParentSequence, owner.Index);
        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        _root.Clear();
        Cursor = new EditorSlot(_root, 0);
        Changed?.Invoke();
    }

    public void SetCursor(EditorSlot slot)
    {
        if (!AllSlots().Contains(slot))
        {
            throw new ArgumentException("Такой позиции нет в программе.", nameof(slot));
        }
        Cursor = slot;
        Changed?.Invoke();
    }

    public bool MoveLeft() => Move(-1);

    public bool MoveRight() => Move(+1);

    /// <summary>Все позиции курсора в порядке чтения программы.</summary>
    public List<EditorSlot> AllSlots()
    {
        var slots = new List<EditorSlot>();
        Collect(_root, slots);
        return slots;
    }

    private bool Move(int delta)
    {
        var slots = AllSlots();
        var i = slots.IndexOf(Cursor) + delta;
        if (i < 0 || i >= slots.Count)
        {
            return false;
        }
        Cursor = slots[i];
        Changed?.Invoke();
        return true;
    }

    private void Insert(EditNode node)
    {
        var (seq, index) = Cursor;
        seq.Insert(index, node);
        Cursor = new EditorSlot(seq, index + 1);
        Changed?.Invoke();
    }

    private static void Collect(List<EditNode> seq, List<EditorSlot> slots)
    {
        slots.Add(new EditorSlot(seq, 0));
        for (var i = 0; i < seq.Count; i++)
        {
            switch (seq[i])
            {
                case EditIf iff:
                    Collect(iff.Then, slots);
                    Collect(iff.Else, slots);
                    break;
                case EditLoop loop:
                    Collect(loop.Body, slots);
                    break;
            }
            slots.Add(new EditorSlot(seq, i + 1));
        }
    }

    private bool IsInside<T>(List<EditNode> seq)
        where T : EditNode
    {
        while (FindOwner(_root, seq) is { } owner)
        {
            if (owner.Node is T)
            {
                return true;
            }
            seq = owner.ParentSequence;
        }
        return false;
    }

    /// <summary>Блок (условие/цикл), которому принадлежит список, и где этот блок лежит.</summary>
    private static Owner? FindOwner(List<EditNode> parent, List<EditNode> target)
    {
        for (var i = 0; i < parent.Count; i++)
        {
            foreach (var child in Children(parent[i]))
            {
                if (ReferenceEquals(child, target))
                {
                    return new Owner(parent, i, parent[i]);
                }
                if (FindOwner(child, target) is { } found)
                {
                    return found;
                }
            }
        }
        return null;
    }

    private static IEnumerable<List<EditNode>> Children(EditNode node) => node switch
    {
        EditIf iff => [iff.Then, iff.Else],
        EditLoop loop => [loop.Body],
        _ => [],
    };

    private static int Count(List<EditNode> seq) => seq.Sum(n => n switch
    {
        EditIf i => 2 + Count(i.Then) + Count(i.Else),
        EditLoop l => 2 + Count(l.Body),
        _ => 1,
    });

    private static List<Instruction> Convert(List<EditNode> seq) => seq.Select<EditNode, Instruction>(n => n switch
    {
        EditCommand c => new CommandInstruction(c.Command),
        EditIf i => new IfRadiationInstruction(Convert(i.Then), Convert(i.Else)),
        EditLoop l => new RepeatInstruction(l.Count, Convert(l.Body)),
        _ => throw new NotSupportedException(n.GetType().Name),
    }).ToList();

    private static void Fill(List<EditNode> target, IReadOnlyList<Instruction> source)
    {
        foreach (var instruction in source)
        {
            switch (instruction)
            {
                case CommandInstruction c:
                    target.Add(new EditCommand(c.Command));
                    break;
                case IfRadiationInstruction i:
                    var iff = new EditIf();
                    Fill(iff.Then, i.Then);
                    Fill(iff.Else, i.Else);
                    target.Add(iff);
                    break;
                case RepeatInstruction r:
                    var loop = new EditLoop(Math.Clamp(r.Count, MinLoopCount, MaxLoopCount));
                    Fill(loop.Body, r.Body);
                    target.Add(loop);
                    break;
            }
        }
    }

    private readonly record struct Owner(List<EditNode> ParentSequence, int Index, EditNode Node);
}
