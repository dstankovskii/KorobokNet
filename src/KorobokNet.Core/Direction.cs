namespace KorobokNet.Core;

/// <summary>
/// Направление робота. Названия из оригинала; на экране (изометрия) это диагонали:
/// West - вправо-вниз, North - влево-вниз, East - влево-вверх, South - вправо-вверх.
/// </summary>
public enum Direction
{
    North,
    East,
    South,
    West,
}

public static class DirectionExtensions
{
    /// <summary>Поворот по часовой стрелке на экране (кнопка "направо").</summary>
    public static Direction TurnRight(this Direction d) => d switch
    {
        Direction.West => Direction.North,
        Direction.North => Direction.East,
        Direction.East => Direction.South,
        Direction.South => Direction.West,
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, null),
    };

    public static Direction TurnLeft(this Direction d) => d switch
    {
        Direction.West => Direction.South,
        Direction.South => Direction.East,
        Direction.East => Direction.North,
        Direction.North => Direction.West,
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, null),
    };

    /// <summary>Смещение на одну клетку вперёд в этом направлении.</summary>
    public static GridPos Offset(this Direction d) => d switch
    {
        Direction.North => new GridPos(-1, 0),
        Direction.South => new GridPos(1, 0),
        Direction.West => new GridPos(0, 1),
        Direction.East => new GridPos(0, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, null),
    };

    public static Direction Parse(string text) => text.Trim().ToLowerInvariant() switch
    {
        "north" => Direction.North,
        "east" => Direction.East,
        "south" => Direction.South,
        "west" => Direction.West,
        _ => throw new FormatException($"Неизвестное направление: '{text}'"),
    };
}
