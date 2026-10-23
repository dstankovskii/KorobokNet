using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KorobokNet.Engine;

/// <summary>Расстояние от точки до границы фигуры: внутри отрицательное, снаружи положительное.</summary>
public delegate float Shape(Vector2 p);

/// <summary>
/// Картинка, нарисованная в памяти и потом загруженная в текстуру. Координаты 0..100 по обеим осям,
/// независимо от размера в пикселях. Края сглаживаются по расстоянию до границы фигуры.
/// </summary>
public sealed class Canvas(int size)
{
    public const float Units = 100f;

    // Холст обходится квадратами по Tile пикселей.
    private const int Tile = 8;

    // Цвета с premultiplied alpha, как ждёт SpriteBatch.
    private readonly Vector4[] _pixels = new Vector4[size * size];

    public int Size { get; } = size;

    public void Fill(Shape shape, Color color)
    {
        var pixelsPerUnit = Size / Units;
        var c = color.ToVector4();
        // Расстояние до фигуры меняется не быстрее, чем расстояние между точками. Поэтому по центру
        // квадрата видно, что он целиком снаружи или целиком внутри, и считать каждый пиксель не надо.
        var reach = (Tile * 0.71f + 1.5f) / pixelsPerUnit;
        for (var ty = 0; ty < Size; ty += Tile)
        {
            var height = Math.Min(Tile, Size - ty);
            for (var tx = 0; tx < Size; tx += Tile)
            {
                var width = Math.Min(Tile, Size - tx);
                var d = shape(new Vector2(tx + width / 2f, ty + height / 2f) / pixelsPerUnit);
                if (d > reach)
                {
                    continue;
                }
                var solid = d < -reach;
                for (var y = ty; y < ty + height; y++)
                {
                    for (var x = tx; x < tx + width; x++)
                    {
                        var coverage = solid
                            ? 1f
                            : Math.Clamp(0.5f - shape(new Vector2(x + 0.5f, y + 0.5f) / pixelsPerUnit) * pixelsPerUnit, 0f, 1f);
                        if (coverage <= 0)
                        {
                            continue;
                        }
                        var a = coverage * c.W;
                        ref var dst = ref _pixels[y * Size + x];
                        dst = new Vector4(c.X * a, c.Y * a, c.Z * a, a) + dst * (1 - a);
                    }
                }
            }
        }
    }

    public Texture2D ToTexture(GraphicsDevice device)
    {
        var data = new Color[_pixels.Length];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = new Color(_pixels[i]);
        }
        var texture = new Texture2D(device, Size, Size);
        texture.SetData(data);
        return texture;
    }
}

/// <summary>Фигуры для <see cref="Canvas"/>. Углы в градусах, по часовой стрелке от оси X (ось Y вниз).</summary>
public static class Shapes
{
    public static Vector2 Polar(Vector2 center, float radius, float degrees)
    {
        var a = MathHelper.ToRadians(degrees);
        return center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
    }

    public static Shape Circle(Vector2 center, float radius) => p => Vector2.Distance(p, center) - radius;

    /// <summary>Эллипс, расстояние приблизительное, но для сглаживания краёв хватает.</summary>
    public static Shape Ellipse(Vector2 center, float rx, float ry) =>
        p => (((p - center) / new Vector2(rx, ry)).Length() - 1) * MathF.Min(rx, ry);

    public static Shape Box(float x, float y, float width, float height, float radius = 0)
    {
        var center = new Vector2(x + width / 2, y + height / 2);
        var half = new Vector2(width / 2 - radius, height / 2 - radius);
        return p =>
        {
            var q = Vector2.Max(p - center, center - p) - half;
            return Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0) - radius;
        };
    }

    /// <summary>Отрезок с круглыми концами.</summary>
    public static Shape Line(Vector2 a, Vector2 b, float width) => p => SegmentDistance(p, a, b) - width / 2;

    /// <summary>Ломаная с круглыми концами и скруглёнными изломами.</summary>
    public static Shape Path(float width, params Vector2[] points) => p =>
    {
        var d = float.MaxValue;
        for (var i = 1; i < points.Length; i++)
        {
            d = MathF.Min(d, SegmentDistance(p, points[i - 1], points[i]));
        }
        return d - width / 2;
    };

    public static Shape Polygon(params Vector2[] v) => p =>
    {
        var d = Vector2.DistanceSquared(p, v[0]);
        var sign = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            var e = v[j] - v[i];
            var w = p - v[i];
            var b = w - e * Math.Clamp(Vector2.Dot(w, e) / Vector2.Dot(e, e), 0f, 1f);
            d = MathF.Min(d, b.LengthSquared());
            var above = p.Y >= v[i].Y;
            var below = p.Y < v[j].Y;
            var left = e.X * w.Y > e.Y * w.X;
            if (above == below && below == left)
            {
                sign = -sign;
            }
        }
        return sign * MathF.Sqrt(d);
    };

    /// <summary>Дуга от <paramref name="from"/> до <paramref name="to"/> с круглыми концами.</summary>
    public static Shape Arc(Vector2 center, float radius, float from, float to, float width)
    {
        var lo = MathHelper.ToRadians(MathF.Min(from, to));
        var hi = MathHelper.ToRadians(MathF.Max(from, to));
        var start = Polar(center, radius, MathF.Min(from, to));
        var end = Polar(center, radius, MathF.Max(from, to));
        return p =>
        {
            var v = p - center;
            var angle = MathF.Atan2(v.Y, v.X);
            while (angle < lo)
            {
                angle += MathF.Tau;
            }
            while (angle >= lo + MathF.Tau)
            {
                angle -= MathF.Tau;
            }
            var d = angle <= hi
                ? MathF.Abs(v.Length() - radius)
                : MathF.Min(Vector2.Distance(p, start), Vector2.Distance(p, end));
            return d - width / 2;
        };
    }

    /// <summary>Дуга со стрелкой на конце <paramref name="to"/>, стрелка смотрит по ходу движения.</summary>
    public static Shape ArcArrow(Vector2 center, float radius, float from, float to, float width)
    {
        var tip = Polar(center, radius, to);
        var radial = Vector2.Normalize(tip - center);
        var tangent = new Vector2(-radial.Y, radial.X) * MathF.Sign(to - from);
        return Union(Arc(center, radius, from, to, width), Head(tip, tangent, width));
    }

    /// <summary>Наконечник стрелки: основание в точке <paramref name="at"/>, острие по <paramref name="direction"/>.</summary>
    public static Shape Head(Vector2 at, Vector2 direction, float width)
    {
        direction = Vector2.Normalize(direction);
        var side = new Vector2(-direction.Y, direction.X) * width * 1.35f;
        var back = at - direction * width * 0.3f;
        var tip = at + direction * width * 1.5f;
        return Round(Polygon(back + side, tip, back - side), width * 0.12f);
    }

    public static Shape Union(params Shape[] shapes) => p =>
    {
        var d = float.MaxValue;
        foreach (var s in shapes)
        {
            d = MathF.Min(d, s(p));
        }
        return d;
    };

    public static Shape Intersect(Shape a, Shape b) => p => MathF.Max(a(p), b(p));

    public static Shape Subtract(Shape a, Shape b) => p => MathF.Max(a(p), -b(p));

    /// <summary>Контур фигуры заданной толщины.</summary>
    public static Shape Outline(Shape shape, float width) => p => MathF.Abs(shape(p)) - width / 2;

    /// <summary>Раздуть фигуру: скруглить углы или получить обводку под ней.</summary>
    public static Shape Round(Shape shape, float amount) => p => shape(p) - amount;

    /// <summary>Фигура, повёрнутая на <paramref name="degrees"/> вокруг <paramref name="center"/>.</summary>
    public static Shape Rotate(Shape shape, Vector2 center, float degrees)
    {
        var a = MathHelper.ToRadians(-degrees);
        var (sin, cos) = MathF.SinCos(a);
        return p =>
        {
            var d = p - center;
            return shape(center + new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos));
        };
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.LengthSquared() < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f);
        return Vector2.Distance(p, a + ab * t);
    }
}
