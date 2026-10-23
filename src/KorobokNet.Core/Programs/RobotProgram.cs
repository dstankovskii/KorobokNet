using System.Text;

namespace KorobokNet.Core.Programs;

/// <summary>
/// Программа робота и её текстовая запись (для сохранений, тестов и демонстраций).
/// Синтаксис: <c>F</c> вперёд, <c>B</c> назад, <c>L</c> налево, <c>R</c> направо,
/// <c>?(A|B)</c> - если впереди радиация, то A, иначе B,
/// <c>3(A)</c> - повторить A три раза. Пробелы необязательны.
/// Пример: <c>R F 3(F L) ?(L F|F F)</c>.
/// </summary>
public sealed class RobotProgram
{
    public static readonly RobotProgram Empty = new([]);

    public RobotProgram(IReadOnlyList<Instruction> instructions)
    {
        Instructions = instructions;
    }

    public IReadOnlyList<Instruction> Instructions { get; }

    public ProgramStats Stats => ProgramStats.Of(Instructions);

    public static RobotProgram Parse(string text)
    {
        var parser = new Parser(text);
        var list = parser.ParseSequence();
        parser.ExpectEnd();
        return new RobotProgram(list);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        Write(sb, Instructions);
        return sb.ToString();
    }

    private static void Write(StringBuilder sb, IReadOnlyList<Instruction> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }
            switch (list[i])
            {
                case CommandInstruction c:
                    sb.Append(c.Command switch
                    {
                        Command.Forward => 'F',
                        Command.Back => 'B',
                        Command.TurnLeft => 'L',
                        Command.TurnRight => 'R',
                        _ => throw new NotSupportedException(c.Command.ToString()),
                    });
                    break;
                case IfRadiationInstruction iff:
                    sb.Append("?(");
                    Write(sb, iff.Then);
                    sb.Append('|');
                    Write(sb, iff.Else);
                    sb.Append(')');
                    break;
                case RepeatInstruction r:
                    sb.Append(r.Count).Append('(');
                    Write(sb, r.Body);
                    sb.Append(')');
                    break;
                default:
                    throw new NotSupportedException(list[i].GetType().Name);
            }
        }
    }

    private sealed class Parser(string text)
    {
        private int _pos;

        public List<Instruction> ParseSequence()
        {
            var list = new List<Instruction>();
            while (true)
            {
                SkipSpaces();
                if (_pos >= text.Length || text[_pos] is ')' or '|')
                {
                    return list;
                }
                list.Add(ParseItem());
            }
        }

        public void ExpectEnd()
        {
            SkipSpaces();
            if (_pos < text.Length)
            {
                throw Error($"лишний символ '{text[_pos]}'");
            }
        }

        private Instruction ParseItem()
        {
            var ch = char.ToUpperInvariant(text[_pos]);
            switch (ch)
            {
                case 'F': _pos++; return new CommandInstruction(Command.Forward);
                case 'B': _pos++; return new CommandInstruction(Command.Back);
                case 'L': _pos++; return new CommandInstruction(Command.TurnLeft);
                case 'R': _pos++; return new CommandInstruction(Command.TurnRight);
                case '?':
                {
                    _pos++;
                    Expect('(');
                    var then = ParseSequence();
                    Expect('|');
                    var otherwise = ParseSequence();
                    Expect(')');
                    return new IfRadiationInstruction(then, otherwise);
                }
                default:
                    if (char.IsAsciiDigit(ch))
                    {
                        var start = _pos;
                        while (_pos < text.Length && char.IsAsciiDigit(text[_pos]))
                        {
                            _pos++;
                        }
                        var count = int.Parse(text.AsSpan(start, _pos - start));
                        if (count < 1)
                        {
                            throw Error("число повторов должно быть не меньше 1");
                        }
                        Expect('(');
                        var body = ParseSequence();
                        Expect(')');
                        return new RepeatInstruction(count, body);
                    }
                    throw Error($"неизвестная команда '{text[_pos]}'");
            }
        }

        private void Expect(char ch)
        {
            SkipSpaces();
            if (_pos >= text.Length || text[_pos] != ch)
            {
                throw Error($"ожидалось '{ch}'");
            }
            _pos++;
        }

        private void SkipSpaces()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos]))
            {
                _pos++;
            }
        }

        private FormatException Error(string message) =>
            new($"Программа \"{text}\", позиция {_pos}: {message}");
    }
}
