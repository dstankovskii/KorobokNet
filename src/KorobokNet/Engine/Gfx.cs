using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KorobokNet.Engine;

public enum Align
{
    Left,
    Center,
    Right,
}

/// <summary>Рисование в виртуальном экране 1920x1080: спрайты, картинки, прямоугольники, текст.</summary>
public sealed class Gfx(SpriteBatch batch, GameAssets assets)
{
    public const int Width = 1920;
    public const int Height = 1080;

    public SpriteBatch Batch { get; } = batch;
    public GameAssets Assets { get; } = assets;

    public void Begin() => Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

    public void End() => Batch.End();

    /// <summary>Спрайт с опорной точкой в <paramref name="position"/>; <paramref name="scale"/> - пикселей экрана на пиксель кадра.</summary>
    public void Sprite(SpriteDef sprite, Vector2 position, float scale, Color? color = null, float rotation = 0f) =>
        Batch.Draw(sprite.Texture, position, sprite.Source, color ?? Color.White, rotation, sprite.Origin, scale, SpriteEffects.None, 0f);

    public void Sprite(string name, Vector2 position, float scale, Color? color = null) =>
        Sprite(Assets.Sprite(name), position, scale, color);

    public void SpriteFit(string name, RectangleF area, Color? color = null)
    {
        var sprite = Assets.Sprite(name);
        var scale = Math.Min(area.Width / sprite.Source.Width, area.Height / sprite.Source.Height);
        var size = sprite.Size * scale;
        var pos = new Vector2(area.X + (area.Width - size.X) / 2, area.Y + (area.Height - size.Y) / 2);
        Batch.Draw(sprite.Texture, pos, sprite.Source, color ?? Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    public void SpriteFill(string name, RectangleF area, Color? color = null)
    {
        var sprite = Assets.Sprite(name);
        Batch.Draw(sprite.Texture, area.ToRectangle(), sprite.Source, color ?? Color.White);
    }

    /// <summary>
    /// Спрайт, натянутый на параллелограмм: углы картинки (0,0)-><paramref name="origin"/>, ось X -> <paramref name="axisX"/>, ось Y -> <paramref name="axisY"/>.
    /// Так "кладутся" на изометрическую клетку знак вопроса и радиация.
    /// </summary>
    public void SpriteSkewed(SpriteDef sprite, Vector2 origin, Vector2 axisX, Vector2 axisY, Color color)
    {
        var m = new Matrix(
            axisX.X / sprite.Source.Width, axisX.Y / sprite.Source.Width, 0, 0,
            axisY.X / sprite.Source.Height, axisY.Y / sprite.Source.Height, 0, 0,
            0, 0, 1, 0,
            origin.X, origin.Y, 0, 1);
        Batch.End();
        Batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, transformMatrix: m);
        Batch.Draw(sprite.Texture, Vector2.Zero, sprite.Source, color);
        Batch.End();
        Begin();
    }

    public void Rect(RectangleF r, Color color) => Batch.Draw(Assets.Pixel, r.ToRectangle(), color);

    public void RectOutline(RectangleF r, Color color, float thickness = 2f)
    {
        Rect(new RectangleF(r.X, r.Y, r.Width, thickness), color);
        Rect(new RectangleF(r.X, r.Bottom - thickness, r.Width, thickness), color);
        Rect(new RectangleF(r.X, r.Y, thickness, r.Height), color);
        Rect(new RectangleF(r.Right - thickness, r.Y, thickness, r.Height), color);
    }

    public void RoundRect(RectangleF r, float radius, Color color)
    {
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        var d = radius * 2;
        var circle = Assets.Circle;
        var half = circle.Width / 2;
        // Четыре четверти круга по углам и три прямоугольника между ними.
        Batch.Draw(circle, new RectangleF(r.X, r.Y, radius, radius).ToRectangle(), new Rectangle(0, 0, half, half), color);
        Batch.Draw(circle, new RectangleF(r.Right - radius, r.Y, radius, radius).ToRectangle(), new Rectangle(half, 0, half, half), color);
        Batch.Draw(circle, new RectangleF(r.X, r.Bottom - radius, radius, radius).ToRectangle(), new Rectangle(0, half, half, half), color);
        Batch.Draw(circle, new RectangleF(r.Right - radius, r.Bottom - radius, radius, radius).ToRectangle(), new Rectangle(half, half, half, half), color);
        Rect(new RectangleF(r.X + radius, r.Y, r.Width - d, r.Height), color);
        Rect(new RectangleF(r.X, r.Y + radius, radius, r.Height - d), color);
        Rect(new RectangleF(r.Right - radius, r.Y + radius, radius, r.Height - d), color);
    }

    /// <summary>Скруглённая панель с рамкой.</summary>
    public void Panel(RectangleF r, Color fill, Color border, float radius = 24f, float borderWidth = 3f)
    {
        RoundRect(r, radius, border);
        RoundRect(r.Inflate(-borderWidth), radius - borderWidth, fill);
    }

    public void Circle(Vector2 center, float radius, Color color) =>
        Batch.Draw(Assets.Circle, new RectangleF(center.X - radius, center.Y - radius, radius * 2, radius * 2).ToRectangle(), color);

    public void Line(Vector2 a, Vector2 b, float thickness, Color color)
    {
        var delta = b - a;
        var length = delta.Length();
        if (length < 0.01f)
        {
            return;
        }
        Batch.Draw(Assets.Pixel, a, null, color, MathF.Atan2(delta.Y, delta.X), new Vector2(0, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public Vector2 MeasureText(string text, float size, bool bold = false) => Assets.Font(size, bold).MeasureString(text);

    /// <summary>Однострочный текст; <paramref name="position"/> - точка выравнивания по горизонтали и верх строки.</summary>
    public void Text(string text, Vector2 position, float size, Color color, Align align = Align.Left, bool bold = false)
    {
        var font = Assets.Font(size, bold);
        var width = font.MeasureString(text).X;
        var x = align switch
        {
            Align.Center => position.X - width / 2,
            Align.Right => position.X - width,
            _ => position.X,
        };
        font.DrawText(Batch, text, new Vector2(MathF.Round(x), MathF.Round(position.Y)), color);
    }

    /// <summary>Текст с переносом по словам, по центру прямоугольника.</summary>
    public void TextBlock(string text, RectangleF area, float size, Color color, bool bold = false, Align align = Align.Center)
    {
        var font = Assets.Font(size, bold);
        var lines = Wrap(font, text, area.Width);
        var lineHeight = size * 1.2f;
        var top = area.Y + (area.Height - lineHeight * lines.Count) / 2;
        foreach (var line in lines)
        {
            var x = align switch
            {
                Align.Center => area.X + area.Width / 2,
                Align.Right => area.Right,
                _ => area.X,
            };
            Text(line, new Vector2(x, top), size, color, align, bold);
            top += lineHeight;
        }
    }

    private static List<string> Wrap(SpriteFontBase font, string text, float width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureString(candidate).X > width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
        }
        return lines;
    }
}
