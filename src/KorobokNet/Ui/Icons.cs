using KorobokNet.Engine;
using Microsoft.Xna.Framework;
using static KorobokNet.Engine.Shapes;

namespace KorobokNet.Ui;

/// <summary>
/// Иконки блоков, мини-карты и кнопок, нарисованные кодом. Каждая рисуется в квадрате 100x100
/// и кешируется под тот размер, в котором её выводят, поэтому на экране она без размытия.
/// </summary>
public static class Icons
{
    // Цвета круглых кнопок.
    public static readonly Color BlueButton = new(52, 140, 220);
    public static readonly Color GrayButton = new(128, 134, 146);
    public static readonly Color DarkButton = new(70, 78, 92);
    public static readonly Color RedButton = new(232, 64, 60);

    /// <summary>Кончик пальца на значке "hand" в долях стороны.</summary>
    public static readonly Vector2 HandTip = new(0.40f, 0.04f);

    private static readonly Color Ink = new(58, 64, 74);
    private static readonly Color Red = new(226, 64, 52);
    private static readonly Color Green = new(76, 175, 80);
    private static readonly Color Leaf = new(84, 170, 60);
    private static readonly Color RobotOutline = new(96, 42, 20);

    public static void Draw(Gfx g, string name, RectangleF area, Color? tint = null, float rotation = 0f)
    {
        var size = Math.Max(8, (int)MathF.Ceiling(Math.Min(area.Width, area.Height)));
        var texture = g.Assets.Painted($"{name}@{size}", () => Paint(name, size));
        g.Texture(texture, area, tint ?? Color.White, rotation);
    }

    private static Canvas Paint(string name, int size)
    {
        var c = new Canvas(size);
        switch (name)
        {
            case "forward":
                c.Fill(Union(Line(new(50, 86), new(50, 44), 16), Head(new(50, 44), -Vector2.UnitY, 16)), Ink);
                break;
            case "back":
                c.Fill(Union(Line(new(50, 14), new(50, 56), 16), Head(new(50, 56), Vector2.UnitY, 16)), Ink);
                break;
            case "turn_left":
                c.Fill(ArcArrow(new(50, 54), 28, 190, -62, 13), Ink);
                break;
            case "turn_right":
                c.Fill(ArcArrow(new(50, 54), 28, -10, 242, 13), Ink);
                break;
            case "loop":
                c.Fill(Union(ArcArrow(new(50, 50), 30, 200, 322, 12), ArcArrow(new(50, 50), 30, 20, 142, 12)), Red);
                break;
            case "loop_end":
                c.Fill(Union(
                    Path(12, new(26, 28), new(60, 28)),
                    Arc(new(60, 49), 21, -90, 90, 12),
                    Path(12, new(60, 70), new(40, 70)),
                    Head(new(40, 70), -Vector2.UnitX, 12)), Red);
                break;
            case "if":
                // Одна дорога расходится на две.
                c.Fill(Union(
                    Line(new(14, 50), new(34, 50), 11),
                    Path(11, new(32, 50), new(54, 26), new(66, 26)),
                    Path(11, new(32, 50), new(54, 74), new(66, 74)),
                    Head(new(66, 26), Vector2.UnitX, 11),
                    Head(new(66, 74), Vector2.UnitX, 11)), Ink);
                break;
            case "if_end":
                // Две ветки сходятся в одну.
                c.Fill(Union(
                    Path(11, new(12, 26), new(34, 26), new(56, 50)),
                    Path(11, new(12, 74), new(34, 74), new(56, 50)),
                    Line(new(54, 50), new(70, 50), 11),
                    Head(new(70, 50), Vector2.UnitX, 11)), Ink);
                break;
            case "start":
                c.Fill(Round(Polygon(new(34, 24), new(78, 50), new(34, 76)), 6), Green);
                break;
            case "radiation":
                PaintRadiation(c);
                break;
            case "stones":
                PaintStones(c);
                break;
            case "soil":
                PaintSoil(c);
                break;
            case "quest":
                c.Fill(Circle(new(50, 50), 44), new Color(70, 40, 90));
                c.Fill(Circle(new(50, 50), 39), Color.White);
                c.Fill(Union(
                    Arc(new(50, 38), 14, 195, 400, 10),
                    Path(10, Polar(new(50, 38), 14, 40), new(50, 58), new(50, 62)),
                    Circle(new(50, 77), 6.5f)), new Color(222, 30, 140));
                break;
            case "crate":
                PaintCrate(c);
                break;
            case "hand":
                PaintHand(c);
                break;
            case "lock":
                c.Fill(Union(
                    Arc(new(50, 40), 17, 180, 360, 10),
                    Line(new(33, 40), new(33, 50), 10),
                    Line(new(67, 40), new(67, 50), 10)), DarkButton);
                c.Fill(Box(20, 46, 60, 46, 10), DarkButton);
                c.Fill(Union(Circle(new(50, 63), 7), Box(46.5f, 63, 7, 16, 2)), new Color(255, 248, 225));
                break;
            case "robot":
                PaintRobot(c);
                break;
            case "wallpaper":
                PaintWallpaper(c);
                break;
            case "pointer":
                // Остриё вправо; куда смотрит робот, задаёт поворот при выводе.
                c.Fill(Round(Polygon(new(26, 18), new(84, 50), new(26, 82)), 9), RobotOutline);
                c.Fill(Round(Polygon(new(30, 26), new(76, 50), new(30, 74)), 3), new Color(255, 206, 64));
                break;
            // Белые значки для круглых кнопок.
            case "cross":
                c.Fill(Union(Line(new(26, 26), new(74, 74), 16), Line(new(74, 26), new(26, 74), 16)), Color.White);
                break;
            case "chevron_left":
                c.Fill(Path(15, new(62, 20), new(34, 50), new(62, 80)), Color.White);
                break;
            case "chevron_right":
                c.Fill(Path(15, new(38, 20), new(66, 50), new(38, 80)), Color.White);
                break;
            case "arrow_left":
                c.Fill(Union(Line(new(84, 50), new(44, 50), 16), Head(new(44, 50), -Vector2.UnitX, 16)), Color.White);
                break;
            case "sound":
                PaintSpeaker(c);
                c.Fill(Union(Arc(new(48, 50), 18, -45, 45, 7), Arc(new(48, 50), 32, -45, 45, 7)), Color.White);
                break;
            case "music":
                // Две восьмые ноты под одним ребром.
                c.Fill(Union(
                    Rotate(Ellipse(new(32, 74), 13, 9.5f), new(32, 74), -20),
                    Rotate(Ellipse(new(70, 66), 13, 9.5f), new(70, 66), -20),
                    Line(new(42, 72), new(42, 26), 7),
                    Line(new(80, 64), new(80, 18), 7),
                    Polygon(new(38.5f, 20), new(83.5f, 11), new(83.5f, 25), new(38.5f, 34))), Color.White);
                break;
            default:
                throw new ArgumentException($"Нет иконки '{name}'.", nameof(name));
        }
        return c;
    }

    /// <summary>Круглая кнопка цвета <paramref name="fill"/> с белым значком.</summary>
    public static Action<Gfx, RectangleF, Color> RoundButton(Color fill, string glyph) => (g, r, tint) =>
    {
        var d = MathF.Min(r.Width, r.Height);
        var center = r.Center;
        g.Circle(center + new Vector2(0, d * 0.05f), d / 2, Gfx.Tint(Color.Black * 0.25f, tint));
        g.Circle(center, d / 2, Gfx.Tint(Color.Lerp(fill, Color.Black, 0.35f), tint));
        g.Circle(center, d / 2 - MathF.Max(3, d * 0.05f), Gfx.Tint(fill, tint));
        Draw(g, glyph, RectangleF.FromCenter(center, d * 0.56f, d * 0.56f), tint);
    };

    private static void PaintSpeaker(Canvas c) =>
        c.Fill(Round(Polygon(new(14, 38), new(28, 38), new(48, 18), new(48, 82), new(28, 62), new(14, 62)), 3), Color.White);

    /// <summary>Рука с указательным пальцем вверх. Кончик пальца в точке <see cref="HandTip"/>.</summary>
    private static void PaintHand(Canvas c)
    {
        var outline = new Color(74, 48, 34);
        var skin = new Color(255, 218, 178);
        const float line = 4f;

        // Большой палец торчит влево-вверх из-за ладони.
        var thumb = Line(new(36, 72), new(21, 55), 15);
        c.Fill(Round(thumb, line), outline);
        c.Fill(thumb, skin);

        // Указательный палец, костяшки согнутых пальцев и ладонь - один силуэт с общим контуром.
        var hand = Union(
            Line(new(40, 14), new(40, 56), 18),
            Line(new(57, 40), new(57, 56), 16),
            Line(new(72, 44), new(72, 58), 15),
            Line(new(85, 50), new(85, 62), 13),
            Box(31, 48, 61, 38, 15));
        c.Fill(Round(hand, line), outline);
        c.Fill(hand, skin);

        // Короткие складки между костяшками.
        c.Fill(Union(
            Line(new(49, 38), new(49, 47), line * 0.8f),
            Line(new(64.8f, 42), new(64.8f, 50), line * 0.8f),
            Line(new(79, 48), new(79, 54), line * 0.8f)), outline);

        var cuff = Box(33, 84, 56, 12, 4);
        c.Fill(Round(cuff, line), outline);
        c.Fill(cuff, BlueButton);
    }

    /// <summary>
    /// Коробок спереди, как на поле: голова с жёлтой щелью, корпус с синим окошком,
    /// колёса по бокам и светлый ковш перед собой.
    /// </summary>
    private static void PaintRobot(Canvas c)
    {
        var body = Box(10, 10, 80, 80, 16);
        c.Fill(Round(body, 4), RobotOutline);
        c.Fill(body, new Color(240, 100, 40));
    }

    /// <summary>
    /// Плитка фона планшета: команды и предметы игры, раскиданные под разными углами.
    /// Рисуется белым, прозрачность задают при выводе. Края плитки стыкуются.
    /// </summary>
    private static void PaintWallpaper(Canvas c)
    {
        (string Kind, float X, float Y, float Angle)[] items =
        [
            ("arrow", 12, 14, -20), ("star", 42, 8, 10), ("gear", 76, 18, 0),
            ("turn", 28, 40, 25), ("crate", 60, 44, -12), ("leaf", 90, 52, 30),
            ("star", 8, 66, -15), ("loop", 40, 74, 0), ("arrow", 74, 80, 160),
            ("dot", 56, 24, 0), ("dot", 20, 90, 0), ("dot", 92, 92, 0), ("plus", 88, 34, 15),
        ];
        var white = Color.White;
        foreach (var (kind, x, y, angle) in items)
        {
            // Фигура у края рисуется ещё раз с другой стороны, чтобы плитки стыковались без шва.
            for (var dx = -100; dx <= 100; dx += 100)
            {
                for (var dy = -100; dy <= 100; dy += 100)
                {
                    var center = new Vector2(x + dx, y + dy);
                    if (center.X < -12 || center.X > 112 || center.Y < -12 || center.Y > 112)
                    {
                        continue;
                    }
                    c.Fill(Rotate(Doodle(kind, center), center, angle), white);
                }
            }
        }
    }

    private static Shape Doodle(string kind, Vector2 p)
    {
        const float w = 1.6f;
        return kind switch
        {
            "arrow" => Union(Line(p + new Vector2(-6, 0), p + new Vector2(3, 0), w), Head(p + new Vector2(3, 0), Vector2.UnitX, w)),
            "turn" => ArcArrow(p, 5, 190, -62, w),
            "loop" => Union(ArcArrow(p, 6, 200, 322, w), ArcArrow(p, 6, 20, 142, w)),
            "star" => Round(Star(p, 6, 2.6f), 0.6f),
            "gear" => Subtract(Union(Circle(p, 4.6f), Teeth(p)), Circle(p, 2)),
            "crate" => Union(
                Subtract(Box(p.X - 5.5f, p.Y - 5.5f, 11, 11, 1.2f), Box(p.X - 3.9f, p.Y - 3.9f, 7.8f, 7.8f, 0.4f)),
                Line(p + new Vector2(-4.5f, -4.5f), p + new Vector2(4.5f, 4.5f), w * 0.9f)),
            "leaf" => Union(Intersect(Circle(p + new Vector2(-3, 0), 6), Circle(p + new Vector2(3, 0), 6)), Line(p + new Vector2(0, 4), p + new Vector2(0, 8), w * 0.8f)),
            "plus" => Union(Line(p + new Vector2(-3, 0), p + new Vector2(3, 0), w), Line(p + new Vector2(0, -3), p + new Vector2(0, 3), w)),
            _ => Circle(p, 1.4f),
        };
    }

    private static Shape Star(Vector2 center, float outer, float inner)
    {
        var points = new Vector2[10];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = Polar(center, i % 2 == 0 ? outer : inner, -90 + i * 36);
        }
        return Polygon(points);
    }

    private static Shape Teeth(Vector2 center)
    {
        var teeth = new Shape[8];
        for (var i = 0; i < teeth.Length; i++)
        {
            teeth[i] = Line(center, Polar(center, 6.4f, i * 45), 2.2f);
        }
        return Union(teeth);
    }

    private static void PaintRadiation(Canvas c)
    {
        var center = new Vector2(50, 50);
        var black = new Color(30, 30, 30);
        c.Fill(Circle(center, 46), black);
        c.Fill(Circle(center, 41), new Color(250, 204, 40));
        c.Fill(Circle(center, 7), black);
        var ring = Subtract(Circle(center, 35), Circle(center, 12));
        foreach (var angle in new[] { -90f, 30f, 150f })
        {
            var wedge = Polygon(center, Polar(center, 60, angle - 30), Polar(center, 60, angle), Polar(center, 60, angle + 30));
            c.Fill(Intersect(ring, wedge), black);
        }
    }

    private static void PaintStones(Canvas c)
    {
        // Три угловатых камня: дальний сверху, два ближних внизу. У каждого светлая верхняя грань.
        var outline = new Color(52, 54, 62);
        Rock(c, outline, new Color(118, 122, 132), new Color(160, 164, 174),
            [new(32, 46), new(40, 26), new(60, 22), new(72, 36), new(66, 52), new(44, 56)], [new(40, 26), new(60, 22), new(72, 36), new(54, 38)]);
        Rock(c, outline, new Color(128, 132, 142), new Color(172, 176, 186),
            [new(12, 72), new(18, 52), new(36, 44), new(52, 56), new(50, 80), new(26, 84)], [new(18, 52), new(36, 44), new(52, 56), new(32, 60)]);
        Rock(c, outline, new Color(110, 114, 124), new Color(152, 156, 166),
            [new(48, 78), new(54, 56), new(72, 48), new(88, 60), new(86, 80), new(66, 88)], [new(54, 56), new(72, 48), new(88, 60), new(70, 64)]);
    }

    private static void Rock(Canvas c, Color outline, Color body, Color top, Vector2[] shape, Vector2[] facet)
    {
        c.Fill(Round(Polygon(shape), 5), outline);
        c.Fill(Round(Polygon(shape), 2), body);
        c.Fill(Intersect(Round(Polygon(facet), 1), Polygon(shape)), top);
    }

    private static void PaintSoil(Canvas c)
    {
        c.Fill(Box(4, 4, 92, 92, 12), new Color(92, 58, 34));
        c.Fill(Box(9, 9, 82, 82, 9), new Color(122, 80, 48));
        foreach (var y in new[] { 28f, 50f, 72f })
        {
            c.Fill(Line(new(22, y), new(78, y), 7), new Color(90, 56, 32));
            c.Fill(Line(new(22, y - 5), new(78, y - 5), 3), new Color(150, 104, 66));
        }
    }

    private static void PaintCrate(Canvas c)
    {
        var wood = new Color(200, 142, 78);
        var dark = new Color(110, 66, 30);
        c.Fill(Box(12, 32, 76, 60, 8), dark);
        c.Fill(Box(17, 37, 66, 50, 4), wood);
        c.Fill(Line(new(17, 54), new(83, 54), 3), dark);
        c.Fill(Line(new(17, 70), new(83, 70), 3), dark);
        c.Fill(Line(new(50, 32), new(50, 12), 5), Leaf);
        c.Fill(Ellipse(new(39, 16), 11, 6), Leaf);
        c.Fill(Ellipse(new(61, 14), 11, 6), Leaf);
    }
}
