namespace KorobokNet.Core.Programs;

/// <summary>
/// Счётчики для оценки, как в оригинальном ProgramKorob.Count():
/// действия считаются по тексту программы (тело цикла - один раз), а не по факту выполнения.
/// </summary>
public readonly record struct ProgramStats(int Actions, int Ifs, int Loops)
{
    public static ProgramStats Of(IEnumerable<Instruction> instructions)
    {
        var stats = new ProgramStats(0, 0, 0);
        foreach (var instruction in instructions)
        {
            stats += instruction switch
            {
                CommandInstruction => new ProgramStats(1, 0, 0),
                IfRadiationInstruction i => new ProgramStats(0, 1, 0) + Of(i.Then) + Of(i.Else),
                RepeatInstruction r => new ProgramStats(0, 0, 1) + Of(r.Body),
                _ => throw new NotSupportedException(instruction.GetType().Name),
            };
        }
        return stats;
    }

    public static ProgramStats operator +(ProgramStats a, ProgramStats b) =>
        new(a.Actions + b.Actions, a.Ifs + b.Ifs, a.Loops + b.Loops);
}
