using KorobokNet.Core;
using KorobokNet.Engine;
using Microsoft.Xna.Framework;

namespace KorobokNet.World;

/// <summary>
/// Геометрия сцены из оригинала. Координаты мира - единицы Unity (камера: высота кадра 10 единиц),
/// экран - виртуальные 1920x1080. Клетка (X, Y) сдвигается на (1.132*(X+Y), 0.42*(X-Y)).
/// </summary>
public static class Iso
{
    public const float PixelsPerUnit = Gfx.Height / 10f;
    public const float Dx = 1.132f;
    public const float Dy = 0.42f;

    // Опорные точки объектов в клетке (0,0) - из MapScript.
    public static readonly Vector2 RobotOrigin = new(-9.334f, 1.08f);
    public static readonly Vector2 CubeOrigin = new(-9.424f, 0.88f);
    public static readonly Vector2 SoilOrigin = new(-8.292f, -0.25f);
    public static readonly Vector2 TrashOrigin = new(-8.31f, -0.36f);

    /// <summary>Центр верхней грани клетки (0,0): верх ромба грядки минус половина высоты ромба.</summary>
    public static readonly Vector2 TileCenterOrigin = new(-8.292f, -0.685f);

    /// <summary>Положение поля (plane.png) в мире.</summary>
    public static readonly Vector2 PlanePosition = new(-0.35f, -0.74f);

    public static Vector2 Step(float x, float y) => new(Dx * (x + y), Dy * (x - y));

    public static Vector2 Cell(Vector2 origin, GridPos p) => origin + Step(p.X, p.Y);

    public static Vector2 ToScreen(Vector2 world) =>
        new(Gfx.Width / 2f + world.X * PixelsPerUnit, Gfx.Height / 2f - world.Y * PixelsPerUnit);

    /// <summary>Множитель для SpriteBatch: пикселей экрана на пиксель кадра при масштабе объекта Unity.</summary>
    public static float Scale(SpriteDef sprite, float unityScale) => PixelsPerUnit / sprite.PixelsPerUnit * unityScale;

    /// <summary>Вектор смещения на одну клетку по X и по Y в пикселях экрана.</summary>
    public static Vector2 ScreenAxisX => new(Dx * PixelsPerUnit, -Dy * PixelsPerUnit);

    public static Vector2 ScreenAxisY => new(Dx * PixelsPerUnit, Dy * PixelsPerUnit);
}
