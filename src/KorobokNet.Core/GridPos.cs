namespace KorobokNet.Core;

/// <summary>
/// Клетка поля. Координаты как в оригинале: поле 8x8, X и Y от 0 до 7,
/// крайние ряды (0 и 7) - непроходимая рамка, игровая зона 1..6.
/// </summary>
public readonly record struct GridPos(int X, int Y)
{
    public static GridPos operator +(GridPos a, GridPos b) => new(a.X + b.X, a.Y + b.Y);

    public static GridPos operator -(GridPos a, GridPos b) => new(a.X - b.X, a.Y - b.Y);

    public override string ToString() => $"({X},{Y})";
}
