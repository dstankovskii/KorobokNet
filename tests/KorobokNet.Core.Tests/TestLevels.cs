using KorobokNet.Core;

namespace KorobokNet.Core.Tests;

internal static class TestLevels
{
    /// <summary>
    /// Уровень из строк в формате levels.json. При направлении west робот едет вправо вдоль строки,
    /// при north - вниз по строкам.
    /// </summary>
    public static LevelDefinition Make(string heading, params string[] rows) =>
        LevelDefinition.FromRows(1, DirectionExtensions.Parse(heading), rows, new StarRule(), new StarRule());

    /// <summary>Клетка по строке и символу в записи уровня.</summary>
    public static GridPos Cell(int row, int column) => new(LevelDefinition.InnerSize - row, column + 1);
}
