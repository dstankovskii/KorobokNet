namespace KorobokNet.Core.Programs;

public enum Command
{
    Forward,
    Back,
    TurnLeft,
    TurnRight,
}

/// <summary>Элемент программы робота.</summary>
public abstract record Instruction;

/// <summary>Простое действие: шаг вперёд/назад или поворот.</summary>
public sealed record CommandInstruction(Command Command) : Instruction;

/// <summary>"Если впереди радиация - <see cref="Then"/>, иначе - <see cref="Else"/>".</summary>
public sealed record IfRadiationInstruction(IReadOnlyList<Instruction> Then, IReadOnlyList<Instruction> Else) : Instruction;

/// <summary>Повторить <see cref="Body"/> <see cref="Count"/> раз.</summary>
public sealed record RepeatInstruction(int Count, IReadOnlyList<Instruction> Body) : Instruction;
