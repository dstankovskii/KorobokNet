using KorobokNet.Core.Programs;

namespace KorobokNet.Core;

/// <summary>
/// Пошаговый исполнитель программы. Каждый <see cref="Step"/> выполняет одно действие
/// (условия и циклы раскрываются по пути) и возвращает события для анимации.
/// Выполнение останавливается, как только все ящики на грядках.
/// </summary>
public sealed class ProgramRunner
{
    private readonly Board _board;
    private readonly Stack<Frame> _stack = new();

    public ProgramRunner(Board board, RobotProgram program)
    {
        _board = board;
        _stack.Push(new Frame(program.Instructions, 1));
        var reveal = board.RevealAhead();
        InitialEvents = reveal is null ? [] : [reveal];
        IsFinished = board.IsWon;
    }

    /// <summary>События до первого шага (робот сразу смотрит перед собой).</summary>
    public IReadOnlyList<GameEvent> InitialEvents { get; }

    public bool IsFinished { get; private set; }

    public bool IsWon => _board.IsWon;

    public int StepsTaken { get; private set; }

    public IReadOnlyList<GameEvent> Step()
    {
        if (IsFinished)
        {
            return [];
        }

        var events = new List<GameEvent>();
        var command = NextCommand(events);
        if (command is null)
        {
            IsFinished = true;
            return events;
        }

        StepsTaken++;
        events.Add(command.Value switch
        {
            Command.Forward => _board.Forward(),
            Command.Back => _board.Back(),
            Command.TurnLeft => _board.TurnLeft(),
            Command.TurnRight => _board.TurnRight(),
            _ => throw new NotSupportedException(command.Value.ToString()),
        });

        if (_board.IsWon)
        {
            IsFinished = true;
            events.Add(new LevelWonEvent());
            return events;
        }

        if (_board.RevealAhead() is { } reveal)
        {
            events.Add(reveal);
        }
        return events;
    }

    /// <summary>Выполнить до конца (для тестов и проверок).</summary>
    public List<GameEvent> RunToEnd()
    {
        var all = new List<GameEvent>(InitialEvents);
        while (!IsFinished)
        {
            all.AddRange(Step());
        }
        return all;
    }

    /// <summary>Следующая команда. Проверки условий по дороге попадают в <paramref name="checks"/>.</summary>
    private Command? NextCommand(List<GameEvent> checks)
    {
        while (_stack.Count > 0)
        {
            var frame = _stack.Peek();
            if (frame.Index >= frame.List.Count)
            {
                if (frame.RemainingRepeats > 1)
                {
                    frame.RemainingRepeats--;
                    frame.Index = 0;
                }
                else
                {
                    _stack.Pop();
                }
                continue;
            }

            var instruction = frame.List[frame.Index++];
            switch (instruction)
            {
                case CommandInstruction c:
                    return c.Command;
                case IfRadiationInstruction i:
                    var radiation = _board.IsRadiationAhead;
                    checks.Add(new ConditionCheckedEvent(_board.Ahead, radiation));
                    _stack.Push(new Frame(radiation ? i.Then : i.Else, 1));
                    break;
                case RepeatInstruction r:
                    _stack.Push(new Frame(r.Body, r.Count));
                    break;
                default:
                    throw new NotSupportedException(instruction.GetType().Name);
            }
        }
        return null;
    }

    private sealed class Frame(IReadOnlyList<Instruction> list, int repeats)
    {
        public IReadOnlyList<Instruction> List { get; } = list;
        public int Index { get; set; }
        public int RemainingRepeats { get; set; } = repeats;
    }
}
