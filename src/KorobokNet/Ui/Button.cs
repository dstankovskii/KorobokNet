using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Ui;

/// <summary>
/// Кнопка: картинка из атласа (<see cref="Sprite"/>), рисунок кодом (<see cref="Painter"/>) или табличка с текстом.
/// Срабатывает при отпускании над кнопкой - так удобно и мышью, и пальцем.
/// </summary>
public sealed class Button
{
    /// <summary>Звук нажатия для всех кнопок (назначает игра).</summary>
    public static Action? ClickSound { get; set; }

    private bool _pressed;
    private bool _hover;

    public Button(RectangleF bounds, string? sprite = null, string? text = null, Action? onClick = null)
    {
        Bounds = bounds;
        Sprite = sprite;
        Text = text;
        OnClick = onClick;
    }

    /// <summary>Зелёная текстовая кнопка основного действия ("Далее", "Готово").</summary>
    public static Button Primary(RectangleF bounds, string text, Action? onClick = null) => new(bounds, text: text, onClick: onClick)
    {
        TextSize = 42,
        Fill = new Color(118, 196, 72),
        Border = new Color(45, 105, 30),
        TextColor = Color.White,
    };

    public RectangleF Bounds { get; set; }
    public string? Sprite { get; set; }

    /// <summary>Рисует кнопку в заданном прямоугольнике с заданной тонировкой (наведение, неактивна).</summary>
    public Action<Gfx, RectangleF, Color>? Painter { get; set; }

    public string? Text { get; set; }
    public float TextSize { get; set; } = 44;
    public Action? OnClick { get; set; }
    public bool Enabled { get; set; } = true;
    public bool Visible { get; set; } = true;

    /// <summary>Подсветка (например, выбранный вариант).</summary>
    public bool Selected { get; set; }

    public Color Fill { get; set; } = new(255, 248, 225);
    public Color Border { get; set; } = new(60, 50, 40);
    public Color TextColor { get; set; } = new(40, 35, 30);

    /// <summary>Подпись под картинкой (для кнопок со спрайтом).</summary>
    public string? Caption { get; set; }

    public bool Update(Input input)
    {
        _hover = Visible && Enabled && Bounds.Contains(input.Pointer);
        if (!Visible || !Enabled)
        {
            _pressed = false;
            return false;
        }
        if (input.PointerPressed && _hover)
        {
            _pressed = true;
            input.ConsumePointer();
            return false;
        }
        if (_pressed && !input.PointerDown)
        {
            _pressed = false;
            if (_hover)
            {
                input.ConsumePointer();
                ClickSound?.Invoke();
                OnClick?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void Draw(Gfx g)
    {
        if (!Visible)
        {
            return;
        }
        var bounds = _pressed && _hover ? Bounds.Scale(0.93f) : Bounds;
        var tint = !Enabled ? Color.White * 0.45f : _hover ? new Color(255, 255, 235) : Color.White;

        if (Sprite is not null || Painter is not null)
        {
            if (Selected)
            {
                g.RoundRect(bounds.Inflate(8), 18, new Color(255, 230, 90) * 0.9f);
            }
            if (Painter is not null)
            {
                Painter(g, bounds, tint);
            }
            else
            {
                g.SpriteFit(Sprite!, bounds, tint);
            }
            if (Text is not null)
            {
                g.TextBlock(Text, bounds, TextSize, TextColor * (Enabled ? 1f : 0.5f), bold: true);
            }
        }
        else
        {
            var fill = Selected ? new Color(255, 225, 120) : Fill;
            g.Panel(bounds, Enabled ? (_hover ? Color.Lerp(fill, Color.White, 0.4f) : fill) : Color.Lerp(fill, Color.Gray, 0.4f), Border * (Enabled ? 1f : 0.5f), 18, 3);
            if (Text is not null)
            {
                g.TextBlock(Text, bounds.Inflate(-10), TextSize, TextColor * (Enabled ? 1f : 0.5f), bold: true);
            }
        }
        if (Caption is not null)
        {
            g.Text(Caption, new Vector2(Bounds.Center.X, Bounds.Bottom + 4), 30, Color.White, Align.Center);
        }
    }
}
