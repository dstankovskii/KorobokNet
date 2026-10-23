namespace KorobokNet.Core;

/// <summary>Кучка камней вокруг поля. Координаты в тех же клетках, что и поле, но за его пределами.</summary>
public sealed record Decoration(GridPos Cell);

public static class DecorationGenerator
{
    public const int MaxCount = 14;

    /// <summary>
    /// Случайные клетки песка вокруг поля, которые видны на экране. Соседние клетки не берутся,
    /// иначе кучки камней сливаются в одну. Одинаковый seed даёт одинаковую картинку,
    /// поэтому seed хранится в сохранении.
    /// </summary>
    public static IReadOnlyList<Decoration> Generate(int seed)
    {
        var cells = new List<GridPos>();
        for (var x = -10; x <= 20; x++)
        {
            for (var y = -10; y <= 20; y++)
            {
                if (Allowed(x, y))
                {
                    cells.Add(new GridPos(x, y));
                }
            }
        }

        var shuffled = cells.ToArray();
        new Random(seed).Shuffle(shuffled);
        var result = new List<Decoration>();
        foreach (var c in shuffled)
        {
            if (result.Count < MaxCount && result.All(d => !Touches(d.Cell, c)))
            {
                result.Add(new Decoration(c));
            }
        }
        return result;
    }

    /// <summary>
    /// На экране клетка (X, Y) сдвигается вправо на X + Y и вверх на X - Y.
    /// Отсюда границы: края экрана, кнопка планшета в правом нижнем углу и середина поля
    /// (X - Y = 0). Выше неё камни выглядят висящими в воздухе.
    /// </summary>
    public static bool Allowed(int x, int y)
    {
        var onBoard = x >= -1 && y >= -1 && x <= LevelDefinition.BoardSize && y <= LevelDefinition.BoardSize;
        var across = x + y;
        var up = x - y;
        var onScreen = across is >= 0 and <= 15 && up is >= -10 and <= 0;
        var underTablet = across >= 12 && up <= -5;
        return !onBoard && onScreen && !underTablet;
    }

    public static bool Touches(GridPos a, GridPos b) => Math.Abs(a.X - b.X) <= 1 && Math.Abs(a.Y - b.Y) <= 1;
}
