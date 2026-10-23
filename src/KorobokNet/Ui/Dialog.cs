using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.Ui;

/// <summary>Окно с сообщением и кнопками поверх экрана; пока открыто, перехватывает весь ввод.</summary>
public sealed class Dialog
{
    private readonly List<Button> _buttons = [];
    private readonly RectangleF _panel;

    public Dialog(string message, params (string Text, Action OnClick)[] buttons)
    {
        Message = message;
        _panel = RectangleF.FromCenter(new Vector2(Gfx.Width / 2f, Gfx.Height / 2f), 1000, 460);
        var width = 340f;
        var gap = 40f;
        var total = buttons.Length * width + (buttons.Length - 1) * gap;
        var x = _panel.Center.X - total / 2;
        foreach (var (text, onClick) in buttons)
        {
            var button = new Button(new RectangleF(x, _panel.Bottom - 140, width, 100), text: text);
            button.OnClick = () =>
            {
                IsOpen = false;
                onClick();
            };
            _buttons.Add(button);
            x += width + gap;
        }
    }

    public string Message { get; }
    public bool IsOpen { get; private set; } = true;

    public void Close() => IsOpen = false;

    public void Update(Input input)
    {
        foreach (var button in _buttons)
        {
            if (button.Update(input))
            {
                break;
            }
        }
        input.ConsumePointer();
    }

    public void Draw(Gfx g)
    {
        g.Rect(new RectangleF(0, 0, Gfx.Width, Gfx.Height), Color.Black * 0.5f);
        g.Panel(_panel, new Color(255, 248, 225), new Color(60, 50, 40), 30, 4);
        g.TextBlock(Message, new RectangleF(_panel.X + 50, _panel.Y + 40, _panel.Width - 100, _panel.Height - 220), 46, new Color(40, 35, 30), bold: true);
        foreach (var button in _buttons)
        {
            button.Draw(g);
        }
    }
}
