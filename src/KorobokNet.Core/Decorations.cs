namespace KorobokNet.Core;

public enum DecorationKind
{
    Trash,
    Tree1,
    Tree2,
}

/// <summary>Украшение вокруг поля. Координаты в тех же клетках, что и поле, но за его пределами.</summary>
public sealed record Decoration(DecorationKind Kind, GridPos Cell);

public static class DecorationGenerator
{
    private const int Attempts = 25;

    /// <summary>
    /// Как в MapScript.Start: 25 случайных клеток вокруг поля; в нижней половине экрана мусор,
    /// в верхней - деревья. Одинаковый seed даёт одинаковую картинку, поэтому seed хранится в сохранении.
    /// </summary>
    public static IReadOnlyList<Decoration> Generate(int seed)
    {
        var random = new Random(seed);
        var result = new List<Decoration>();
        for (var k = 0; k < Attempts; k++)
        {
            var x = random.Next(-5, 15);
            var y = random.Next(-7, 13);
            if (x >= -1 && y >= -1 && x <= LevelDefinition.BoardSize - 1 && y <= LevelDefinition.BoardSize - 1)
            {
                continue;
            }

            // В оригинале: экранный Y = -0.36 + 0.42*(X - Y) < 0, то есть X - Y ≤ 0.
            var kind = x - y <= 0
                ? DecorationKind.Trash
                : random.Next(0, 2) == 0 ? DecorationKind.Tree1 : DecorationKind.Tree2;
            result.Add(new Decoration(kind, new GridPos(x, y)));
        }
        return result;
    }
}
