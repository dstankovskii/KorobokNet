using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Ui;

/// <summary>Блок команды планшета: белая карточка с цветной шапкой и иконкой.</summary>
public static class Blocks
{
    /// <summary>Шапка команд движения.</summary>
    public static readonly Color Action = new(104, 180, 72);

    /// <summary>Шапка условий, циклов и старта.</summary>
    public static readonly Color Control = new(246, 150, 46);

    private static readonly Color Edge = new(72, 80, 92);
    private static readonly Color Face = new(250, 250, 248);

    /// <param name="label">Подпись в шапке. Пустая строка - шапка той же высоты, но без текста.</param>
    public static void Draw(Gfx g, RectangleF r, Color accent, string? icon, Color? tint = null, string? label = null)
    {
        var t = tint ?? Color.White;
        var radius = r.Width * 0.12f;
        var border = MathF.Max(2, r.Width * 0.025f);
        var inner = r.Inflate(-border);
        var innerRadius = radius - border;
        var header = HeaderShare(label) * inner.Height;
        var face = new RectangleF(inner.X, inner.Y + header, inner.Width, inner.Height - header);

        g.RoundRect(r.Offset(new Vector2(0, r.Height * 0.035f)), radius, Gfx.Tint(Color.Black * 0.2f, t));
        g.RoundRect(r, radius, Gfx.Tint(Edge, t));
        g.RoundRect(inner, innerRadius, Gfx.Tint(accent, t));
        g.RoundRect(face, innerRadius, Gfx.Tint(Face, t));
        g.Rect(new RectangleF(face.X, face.Y, face.Width, innerRadius), Gfx.Tint(Face, t));
        g.Rect(new RectangleF(face.X, face.Y, face.Width, border), Gfx.Tint(Color.Lerp(accent, Color.Black, 0.3f), t));

        if (icon is not null)
        {
            var side = MathF.Min(face.Width, face.Height) * 0.8f;
            Icons.Draw(g, icon, RectangleF.FromCenter(face.Center, side, side), t);
        }
        if (!string.IsNullOrEmpty(label))
        {
            // Длинную подпись ужимаем, чтобы влезла в шапку.
            var size = header * 0.62f;
            var width = g.MeasureText(label, size, bold: true).X;
            size = MathF.Floor(size * MathF.Min(1, (inner.Width - border * 4) / width));
            var top = inner.Y + (header - size * 1.2f) / 2;
            var center = inner.Center.X;
            g.Text(label, new Vector2(center, top + 1.5f), size, Gfx.Tint(Color.Lerp(accent, Color.Black, 0.45f), t), Align.Center, bold: true);
            g.Text(label, new Vector2(center, top), size, Gfx.Tint(Color.White, t), Align.Center, bold: true);
        }
    }

    /// <summary>Доля высоты, которую занимает цветная шапка.</summary>
    public static float HeaderShare(string? label) => label is null ? 0.16f : 0.25f;
}
