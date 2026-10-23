using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace KorobokNet.Engine;

/// <summary>
/// Ввод в координатах виртуального экрана: одна "указка" (мышь или первый палец), клавиатура и набранный текст.
/// Нажатие можно "поглотить" (<see cref="ConsumePointer"/>), чтобы его не обработал второй элемент под ним.
/// </summary>
public sealed class Input
{
    private readonly List<char> _typed = [];
    private readonly List<char> _pendingTyped = [];
    private KeyboardState _keys;
    private KeyboardState _prevKeys;
    private int _prevWheel;
    private int _prevWheelX;
    private bool _wasDown;

    public Vector2 Pointer { get; private set; }
    public bool PointerDown { get; private set; }
    public bool PointerPressed { get; private set; }
    public bool PointerReleased { get; private set; }

    /// <summary>Прокрутка колесом за кадр (положительная - вверх).</summary>
    public int Wheel { get; private set; }

    /// <summary>Прокрутка вбок за кадр (тачпад, наклон колеса; положительная - вправо).</summary>
    public int WheelX { get; private set; }

    /// <summary>Символы, набранные за кадр (включая '\b' и '\r').</summary>
    public IReadOnlyList<char> Typed => _typed;

    public bool IsScripted { get; private set; }

    public void OnTextInput(char c) => _pendingTyped.Add(c);

    public void Update(Func<Vector2, Vector2> screenToVirtual, bool windowActive)
    {
        _prevKeys = _keys;
        _keys = windowActive ? Keyboard.GetState() : default;
        _typed.Clear();
        _typed.AddRange(_pendingTyped);
        _pendingTyped.Clear();
        IsScripted = false;

        var touches = TouchPanel.GetState();
        bool down;
        if (touches.Count > 0)
        {
            Pointer = screenToVirtual(touches[0].Position);
            down = touches[0].State is TouchLocationState.Pressed or TouchLocationState.Moved;
        }
        else
        {
            var mouse = Mouse.GetState();
            Pointer = screenToVirtual(new Vector2(mouse.X, mouse.Y));
            down = windowActive && mouse.LeftButton == ButtonState.Pressed;
            Wheel = mouse.ScrollWheelValue - _prevWheel;
            _prevWheel = mouse.ScrollWheelValue;
            WheelX = mouse.HorizontalScrollWheelValue - _prevWheelX;
            _prevWheelX = mouse.HorizontalScrollWheelValue;
        }
        SetPointer(down);
    }

    /// <summary>Управление указкой из сценария (живые демонстрации): реальный ввод при этом игнорируется.</summary>
    public void Script(Vector2 position, bool down)
    {
        IsScripted = true;
        Pointer = position;
        Wheel = 0;
        WheelX = 0;
        _typed.Clear();
        SetPointer(down);
    }

    public bool KeyPressed(Keys key) => _keys.IsKeyDown(key) && !_prevKeys.IsKeyDown(key);

    public bool KeyDown(Keys key) => _keys.IsKeyDown(key);

    public void ConsumePointer()
    {
        PointerPressed = false;
        PointerReleased = false;
    }

    private void SetPointer(bool down)
    {
        PointerDown = down;
        PointerPressed = down && !_wasDown;
        PointerReleased = !down && _wasDown;
        _wasDown = down;
    }
}
