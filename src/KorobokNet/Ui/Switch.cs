using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Ui;

/// <summary>Переключатель OFF/ON. Все размеры считаются от высоты, поэтому он рисуется любого размера.</summary>
public static class Switch
{
    /// <param name="knob">Положение рычажка: 0 - OFF, 1 - ON, между ними - рычажок в пути.</param>
    public static void Draw(Gfx g, RectangleF r, float knob, Color tint)
    {
        var track = RectangleF.FromCenter(r.Center, r.Width * 0.9f, r.Height * 0.78f);
        var inner = track.Inflate(-r.Height * 0.045f);
        var radius = inner.Height / 2;
        var shadow = new Vector2(0, r.Height * 0.05f);
        g.RoundRect(track.Offset(shadow), track.Height / 2, Gfx.Tint(Color.Black * 0.25f, tint));
        g.RoundRect(track, track.Height / 2, Gfx.Tint(new Color(40, 44, 52), tint));
        g.RoundRect(inner, radius, Gfx.Tint(Color.Lerp(new Color(120, 126, 138), new Color(84, 180, 70), knob), tint));

        var center = new Vector2(MathHelper.Lerp(inner.X + radius, inner.Right - radius, knob), inner.Center.Y);
        var free = knob < 0.5f
            ? new RectangleF(inner.X + radius * 2, inner.Y, inner.Width - radius * 2, inner.Height)
            : new RectangleF(inner.X, inner.Y, inner.Width - radius * 2, inner.Height);
        g.TextBlock(knob < 0.5f ? "OFF" : "ON", free, r.Height * 0.4f, Gfx.Tint(Color.White, tint), bold: true);
        var knobRadius = radius - r.Height * 0.05f;
        g.Circle(center + shadow * 0.6f, knobRadius, Gfx.Tint(Color.Black * 0.25f, tint));
        g.Circle(center, knobRadius, Gfx.Tint(Color.White, tint));
    }
}
