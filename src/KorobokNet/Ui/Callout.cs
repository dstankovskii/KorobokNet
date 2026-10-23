using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Ui;

/// <summary>
/// Табличка с хвостиком, которая встаёт рядом с элементом интерфейса и указывает на него.
/// Сторона выбирается сама, лишь бы табличка поместилась в отведённую область.
/// </summary>
public static class Callout
{
    private const float Padding = 34;
    private const float Radius = 26;
    private const float BorderWidth = 4;
    private const float TailLength = 44;
    private const float TailWidth = 60;
    private const float Gap = 6;

    public static readonly Color Fill = new(255, 242, 210);
    public static readonly Color Border = new(40, 35, 30);
    public static readonly Color Ink = new(30, 25, 20);

    private enum Side
    {
        Left,
        Right,
        Above,
        Below,
    }

    public static void Draw(Gfx g, string text, RectangleF target, RectangleF room, float textSize = 46, float maxWidth = 860)
    {
        var size = g.MeasureBlock(text, textSize, maxWidth - Padding * 2, bold: true) + new Vector2(Padding * 2 + 2, Padding * 2);
        foreach (var side in Sides(target, room))
        {
            if (Place(target, room, size, side) is { } place)
            {
                DrawTail(g, place.From, place.Tip, Border, outer: true);
                g.Panel(place.Panel, Fill, Border, Radius, BorderWidth);
                DrawTail(g, place.From, place.Tip, Fill, outer: false);
                g.TextBlock(text, place.Panel.Inflate(-Padding), textSize, Ink, bold: true);
                return;
            }
        }
        // Места рядом нет, табличка просто встаёт сверху.
        var panel = RectangleF.FromCenter(new Vector2(room.Center.X, room.Y + size.Y / 2), size.X, size.Y);
        g.Panel(panel, Fill, Border, Radius, BorderWidth);
        g.TextBlock(text, panel.Inflate(-Padding), textSize, Ink, bold: true);
    }

    private static Side[] Sides(RectangleF target, RectangleF room)
    {
        var d = target.Center - room.Center;
        var x = d.X > 0 ? Side.Left : Side.Right;
        var y = d.Y > 0 ? Side.Above : Side.Below;
        var flipX = x == Side.Left ? Side.Right : Side.Left;
        var flipY = y == Side.Above ? Side.Below : Side.Above;
        // Сбоку места обычно больше, поэтому бок пробуем первым, если элемент не сильно ближе к верху или низу.
        var sideways = Math.Abs(d.X) * 2 / room.Width > Math.Abs(d.Y) * 2 / room.Height - 0.1f;
        return sideways ? [x, y, flipX, flipY] : [y, x, flipY, flipX];
    }

    private static (RectangleF Panel, Vector2 From, Vector2 Tip)? Place(RectangleF target, RectangleF room, Vector2 size, Side side)
    {
        var horizontal = side is Side.Left or Side.Right;
        var x = side switch
        {
            Side.Left => target.X - TailLength - size.X,
            Side.Right => target.Right + TailLength,
            _ => target.Center.X - size.X / 2,
        };
        var y = side switch
        {
            Side.Above => target.Y - TailLength - size.Y,
            Side.Below => target.Bottom + TailLength,
            _ => target.Center.Y - size.Y / 2,
        };
        var fits = horizontal
            ? x >= room.X && x + size.X <= room.Right
            : y >= room.Y && y + size.Y <= room.Bottom;
        if (!fits)
        {
            return null;
        }
        var panel = new RectangleF(Clamp(x, room.X, room.Right - size.X), Clamp(y, room.Y, room.Bottom - size.Y), size.X, size.Y);

        // Хвостик не должен вылезать на скруглённые углы таблички.
        const float inset = Radius + TailWidth / 2;
        if (horizontal)
        {
            var along = Clamp(target.Center.Y, panel.Y + inset, panel.Bottom - inset);
            var tipY = Clamp(along, target.Y + 10, target.Bottom - 10);
            return side == Side.Left
                ? (panel, new Vector2(panel.Right, along), new Vector2(target.X - Gap, tipY))
                : (panel, new Vector2(panel.X, along), new Vector2(target.Right + Gap, tipY));
        }
        var across = Clamp(target.Center.X, panel.X + inset, panel.Right - inset);
        var tipX = Clamp(across, target.X + 10, target.Right - 10);
        return side == Side.Above
            ? (panel, new Vector2(across, panel.Bottom), new Vector2(tipX, target.Y - Gap))
            : (panel, new Vector2(across, panel.Y), new Vector2(tipX, target.Bottom + Gap));
    }

    /// <summary>
    /// Хвостик в два слоя, как и сама панель: контур рисуется до панели,
    /// заливка после, чтобы перекрыть рамку в месте стыка.
    /// </summary>
    private static void DrawTail(Gfx g, Vector2 from, Vector2 tip, Color color, bool outer)
    {
        var dir = Vector2.Normalize(tip - from);
        var root = from - dir * BorderWidth * 2;
        var length = Vector2.Distance(root, tip);
        var half = TailWidth / 2 + BorderWidth;
        if (outer)
        {
            g.Triangle(root, tip, half * 2, color);
            return;
        }
        // Заливка - тот же треугольник, у которого все стороны сдвинуты внутрь на толщину рамки.
        var angle = MathF.Atan2(half, length);
        var innerTip = tip - dir * (BorderWidth / MathF.Sin(angle));
        var innerRoot = from - dir * (BorderWidth + 1);
        var innerHalf = Vector2.Distance(innerRoot, tip) * MathF.Tan(angle) - BorderWidth / MathF.Cos(angle);
        g.Triangle(innerRoot, innerTip, innerHalf * 2, color);
    }

    private static float Clamp(float value, float min, float max) => min > max ? (min + max) / 2 : Math.Clamp(value, min, max);
}
