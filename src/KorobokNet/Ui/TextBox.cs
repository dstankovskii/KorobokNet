using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Ui;

/// <summary>Однострочное поле ввода (имя игрока).</summary>
public sealed class TextBox(RectangleF bounds, string placeholder, int maxLength = 20)
{
    private float _blink;

    public RectangleF Bounds { get; set; } = bounds;
    public string Text { get; set; } = "";
    public string Placeholder { get; } = placeholder;
    public bool Focused { get; set; } = true;
    public Action? OnSubmit { get; set; }

    public void Update(float dt, Input input)
    {
        _blink += dt;
        if (input.PointerPressed)
        {
            Focused = Bounds.Contains(input.Pointer);
        }
        if (!Focused)
        {
            return;
        }
        foreach (var c in input.Typed)
        {
            if (c == '\b')
            {
                if (Text.Length > 0)
                {
                    Text = Text[..^1];
                }
            }
            else if (c is '\r' or '\n')
            {
                OnSubmit?.Invoke();
            }
            else if ((char.IsLetterOrDigit(c) || c is ' ' or '-' or '_') && Text.Length < maxLength)
            {
                Text += c;
            }
            _blink = 0;
        }
    }

    public void Draw(Gfx g)
    {
        g.Panel(Bounds, Color.White, Focused ? new Color(70, 130, 200) : new Color(120, 120, 120), 14, 3);
        var size = Bounds.Height * 0.5f;
        var textPos = new Vector2(Bounds.X + 20, Bounds.Center.Y - size * 0.62f);
        if (Text.Length == 0)
        {
            g.Text(Placeholder, textPos, size, new Color(150, 150, 150));
        }
        else
        {
            g.Text(Text, textPos, size, new Color(30, 30, 30));
        }
        if (Focused && (int)(_blink * 2) % 2 == 0)
        {
            var x = textPos.X + (Text.Length == 0 ? 0 : g.MeasureText(Text, size).X) + 3;
            g.Rect(new RectangleF(x, Bounds.Y + Bounds.Height * 0.2f, 3, Bounds.Height * 0.6f), new Color(30, 30, 30));
        }
    }
}
