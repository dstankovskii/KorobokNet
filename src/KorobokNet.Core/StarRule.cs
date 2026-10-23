using KorobokNet.Core.Programs;

namespace KorobokNet.Core;

/// <summary>Условие на звёзды. Пустое поле - без ограничения.</summary>
public sealed record StarRule(
    int? MinActions = null,
    int? MaxActions = null,
    int? MinIfs = null,
    int? MaxIfs = null,
    int? MinLoops = null,
    int? MaxLoops = null)
{
    public bool Matches(ProgramStats stats) =>
        InRange(stats.Actions, MinActions, MaxActions)
        && InRange(stats.Ifs, MinIfs, MaxIfs)
        && InRange(stats.Loops, MinLoops, MaxLoops);

    private static bool InRange(int value, int? min, int? max) =>
        (min is null || value >= min) && (max is null || value <= max);
}
