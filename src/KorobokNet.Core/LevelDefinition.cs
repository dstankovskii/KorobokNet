using KorobokNet.Core.Programs;

namespace KorobokNet.Core;

public sealed class LevelDefinition
{
    public const int BoardSize = 8;
    public const int InnerSize = 6;

    public required int Number { get; init; }
    public required Direction StartHeading { get; init; }
    public required GridPos Robot { get; init; }
    public required IReadOnlyList<GridPos> Cubes { get; init; }
    public required IReadOnlyList<GridPos> Soil { get; init; }
    public required IReadOnlyList<GridPos> Trash { get; init; }

    /// <summary>Клетки "?": половина из них при запуске уровня становится радиацией.</summary>
    public required IReadOnlyList<GridPos> Quests { get; init; }

    public required StarRule ThreeStars { get; init; }
    public required StarRule TwoStars { get; init; }

    /// <summary>Эталонное решение на 3 звезды: для тестов и живой демонстрации.</summary>
    public RobotProgram? Solution { get; init; }

    public static bool IsBorder(GridPos p) =>
        p.X <= 0 || p.Y <= 0 || p.X >= BoardSize - 1 || p.Y >= BoardSize - 1;

    public int Score(ProgramStats stats) =>
        ThreeStars.Matches(stats) ? 3 : TwoStars.Matches(stats) ? 2 : 1;

    /// <summary>
    /// Разбор карты в формате оригинального ObjectPosition: 6 строк по 6 символов.
    /// Строка i (с 0) - это X = 6 - i, символ j - это Y = j + 1 (так их раскладывал MapScript.MapReader).
    /// </summary>
    public static LevelDefinition FromRows(
        int number,
        Direction heading,
        IReadOnlyList<string> rows,
        StarRule three,
        StarRule two,
        RobotProgram? solution = null)
    {
        if (rows.Count != InnerSize || rows.Any(r => r.Length != InnerSize))
        {
            throw new FormatException($"Уровень {number}: нужно {InnerSize} строк по {InnerSize} символов.");
        }

        GridPos? robot = null;
        var cubes = new List<GridPos>();
        var soil = new List<GridPos>();
        var trash = new List<GridPos>();
        var quests = new List<GridPos>();

        for (var i = 0; i < InnerSize; i++)
        {
            for (var j = 0; j < InnerSize; j++)
            {
                var pos = new GridPos(InnerSize - i, j + 1);
                switch (rows[i][j])
                {
                    case 'k':
                        if (robot is not null)
                        {
                            throw new FormatException($"Уровень {number}: больше одного робота.");
                        }
                        robot = pos;
                        break;
                    case 'c': cubes.Add(pos); break;
                    case 's': soil.Add(pos); break;
                    case 't': trash.Add(pos); break;
                    case '?': quests.Add(pos); break;
                    case '0': break;
                    default:
                        throw new FormatException($"Уровень {number}: неизвестный символ '{rows[i][j]}'.");
                }
            }
        }

        if (robot is null)
        {
            throw new FormatException($"Уровень {number}: нет робота.");
        }
        if (cubes.Count == 0 || soil.Count < cubes.Count)
        {
            throw new FormatException($"Уровень {number}: нужен хотя бы один ящик и грядок не меньше, чем ящиков.");
        }

        return new LevelDefinition
        {
            Number = number,
            StartHeading = heading,
            Robot = robot.Value,
            Cubes = cubes,
            Soil = soil,
            Trash = trash,
            Quests = quests,
            ThreeStars = three,
            TwoStars = two,
            Solution = solution,
        };
    }
}
