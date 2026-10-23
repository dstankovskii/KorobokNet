using Microsoft.Xna.Framework;

namespace KorobokNet.Engine;

/// <summary>Прямоугольник в виртуальных координатах (float).</summary>
public readonly record struct RectangleF(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public Vector2 Center => new(X + Width / 2, Y + Height / 2);
    public Vector2 Position => new(X, Y);
    public Vector2 Size => new(Width, Height);

    public static RectangleF FromCenter(Vector2 center, float width, float height) =>
        new(center.X - width / 2, center.Y - height / 2, width, height);

    public bool Contains(Vector2 p) => p.X >= X && p.X < Right && p.Y >= Y && p.Y < Bottom;

    public RectangleF Inflate(float amount) => new(X - amount, Y - amount, Width + amount * 2, Height + amount * 2);

    public RectangleF Offset(Vector2 by) => new(X + by.X, Y + by.Y, Width, Height);

    /// <summary>Наименьший прямоугольник, в который помещаются оба.</summary>
    public static RectangleF Union(RectangleF a, RectangleF b)
    {
        var x = Math.Min(a.X, b.X);
        var y = Math.Min(a.Y, b.Y);
        return new(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Bottom, b.Bottom) - y);
    }

    /// <summary>Масштаб относительно центра.</summary>
    public RectangleF Scale(float factor) => FromCenter(Center, Width * factor, Height * factor);

    public Rectangle ToRectangle() =>
        new((int)MathF.Round(X), (int)MathF.Round(Y), (int)MathF.Round(Width), (int)MathF.Round(Height));
}
