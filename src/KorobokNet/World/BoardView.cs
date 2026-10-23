using System.Collections;
using KorobokNet.Core;
using KorobokNet.Engine;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace KorobokNet.World;

/// <summary>
/// Поле уровня на экране: рисует состояние <see cref="Board"/> и проигрывает события
/// исполнителя (<see cref="GameEvent"/>) как анимации - повороты, наклоны, езду, толкание ящиков, рост растения.
/// Тайминги взяты из оригинального KorobController.
/// </summary>
public sealed class BoardView
{
    private const float RobotScale = 0.7f;
    private const float CubeScale = 0.75f;
    private const float Pause = 0.4f;
    private const float MoveSeconds = 0.6f;

    // Мусор и деревья в оригинале чуть повёрнуты (-2.6° в Unity).
    private const float DecorRotation = 0.0454f;

    private readonly GameAssets _assets;
    private readonly Audio _audio;
    private readonly Board _board;
    private readonly Actor _robot;
    private readonly Dictionary<GridPos, Actor> _cubes = [];
    private readonly Dictionary<GridPos, float> _questAlpha = [];
    private readonly Dictionary<GridPos, float> _radiationAlpha = [];
    private readonly List<(string Sprite, Vector2 World, float Scale, float Depth)> _decorations = [];
    private Motion _motion = Motion.None;

    // Board меняет направление сразу при исполнении команды, а робот на экране поворачивается позже,
    // поэтому анимации берут направление отсюда.
    private Direction _heading;

    public BoardView(GameAssets assets, Audio audio, Board board, int decorationSeed)
    {
        _assets = assets;
        _audio = audio;
        _board = board;
        _heading = board.Heading;
        _robot = new Actor(Iso.Cell(Iso.RobotOrigin, board.Robot), _heading.Name());
        foreach (var cube in board.Cubes)
        {
            _cubes[cube] = new Actor(Iso.Cell(Iso.CubeOrigin, cube), "animation_cube");
        }
        foreach (var quest in board.HiddenQuests)
        {
            _questAlpha[quest] = 1f;
        }
        foreach (var d in DecorationGenerator.Generate(decorationSeed))
        {
            var world = Iso.Cell(Iso.TrashOrigin, d.Cell);
            var depth = CellDepth(d.Cell);
            _decorations.Add(d.Kind switch
            {
                DecorationKind.Trash => ("trash_plane", world + new Vector2(0.32f, 0.17f), 0.72f, depth),
                DecorationKind.Tree1 => ("tree_0", world + new Vector2(0.32f, 0.17f), 1.3f, depth),
                _ => ("tree_1", world + new Vector2(0.32f, 0f), 1f, depth),
            });
        }
    }

    public Board Board => _board;

    /// <summary>Экранный прямоугольник поля (для стрелок в обучении).</summary>
    public static RectangleF ScreenBounds
    {
        get
        {
            var center = Iso.ToScreen(Iso.PlanePosition);
            return RectangleF.FromCenter(center, 1400, 560);
        }
    }

    public void Update(float dt)
    {
        _robot.Time += dt;
        foreach (var cube in _cubes.Values)
        {
            cube.Time += dt;
        }
    }

    /// <summary>Выполняет программу по шагам; <paramref name="onFinished"/> получает true при победе.</summary>
    public IEnumerator Run(ProgramRunner runner, Action<bool> onFinished)
    {
        foreach (var e in runner.InitialEvents)
        {
            yield return Animate(e);
        }
        yield return Pause;

        while (!runner.IsFinished)
        {
            foreach (var e in runner.Step())
            {
                yield return Animate(e);
            }
        }

        if (!runner.IsWon)
        {
            yield return StopMotion();
            onFinished(false);
            yield break;
        }
        yield return Grow();
        onFinished(true);
    }

    public void Draw(Gfx g)
    {
        g.Sprite(_assets.Sprite("plane"), Iso.ToScreen(Iso.PlanePosition), Iso.Scale(_assets.Sprite("plane"), 0.75f));

        var soil = _assets.Sprite("soil_plane");
        foreach (var cell in _board.Level.Soil)
        {
            g.Sprite(soil, Iso.ToScreen(Iso.Cell(Iso.SoilOrigin, cell)), Iso.Scale(soil, 0.75f));
        }
        foreach (var (cell, alpha) in _questAlpha)
        {
            if (alpha > 0)
            {
                DrawDecal(g, "question", cell, 0.62f, alpha);
            }
        }
        foreach (var (cell, alpha) in _radiationAlpha)
        {
            DrawDecal(g, "radiation", cell, 0.8f, alpha);
        }

        // Сортируем по клетке, а не по Y спрайта: у робота, ящика и камней разные опорные точки.
        var items = new List<(float Depth, Action Draw)>();
        var trash = _assets.Sprite("trash_plane");
        foreach (var cell in _board.Level.Trash)
        {
            var world = Iso.Cell(Iso.TrashOrigin, cell);
            items.Add((CellDepth(cell), () => DrawRotated(g, trash, world, new Vector2(0.72f, 0.6f))));
        }
        foreach (var (sprite, world, scale, depth) in _decorations)
        {
            var def = _assets.Sprite(sprite);
            var size = sprite == "trash_plane" ? new Vector2(0.72f, 0.6f) : new Vector2(scale);
            items.Add((depth, () => DrawRotated(g, def, world, size)));
        }
        foreach (var cube in _cubes.Values)
        {
            items.Add((cube.World.Y - Iso.CubeOrigin.Y, () => DrawActor(g, cube, CubeScale)));
        }
        items.Add((_robot.World.Y - Iso.RobotOrigin.Y, () => DrawActor(g, _robot, RobotScale)));

        foreach (var (_, draw) in items.OrderByDescending(i => i.Depth))
        {
            draw();
        }
    }

    private IEnumerator Animate(GameEvent e)
    {
        switch (e)
        {
            case MovedEvent moved:
                yield return Move(moved);
                break;
            case TurnedEvent turned:
                yield return StopMotion();
                _audio.SetMotor(true);
                yield return Play($"rot_{turned.From.Name()}-{turned.To.Name()}");
                _heading = turned.To;
                _robot.SetClip(_heading.Name());
                _audio.SetMotor(false);
                yield return Pause / 2;
                break;
            case BlockedEvent blocked:
                yield return Bump(blocked);
                break;
            case QuestRevealedEvent reveal:
                yield return Reveal(reveal);
                break;
            case LevelWonEvent:
                yield return StopMotion();
                break;
        }
    }

    private IEnumerator Move(MovedEvent moved)
    {
        var heading = _heading.Name();
        var wanted = moved.Backward ? Motion.Back : Motion.Forward;
        if (_motion != wanted)
        {
            yield return StopMotion();
            yield return Play($"incline_{heading}");
            yield return Pause - 0.25f;
        }
        _motion = wanted;
        _robot.SetClip((moved.Backward ? "re-go_" : "go_") + heading);
        _audio.SetMotor(true);

        Actor? cube = null;
        Vector2 cubeFrom = default, cubeTo = default;
        if (moved.Push is { } push && _cubes.Remove(push.From, out cube))
        {
            _cubes[push.To] = cube;
            cubeFrom = Iso.Cell(Iso.CubeOrigin, push.From);
            cubeTo = Iso.Cell(Iso.CubeOrigin, push.To);
        }
        var from = Iso.Cell(Iso.RobotOrigin, moved.From);
        var to = Iso.Cell(Iso.RobotOrigin, moved.To);
        for (var t = 0f; t < MoveSeconds; t += Frame.Dt)
        {
            var k = t / MoveSeconds;
            _robot.World = Vector2.Lerp(from, to, k);
            if (cube is not null)
            {
                cube.World = Vector2.Lerp(cubeFrom, cubeTo, k);
            }
            yield return null;
        }
        _robot.World = to;
        if (cube is not null)
        {
            cube.World = cubeTo;
        }
    }

    /// <summary>Робот упёрся: наклон, толчок в сторону препятствия и назад.</summary>
    private IEnumerator Bump(BlockedEvent blocked)
    {
        var heading = _heading.Name();
        if (_motion != Motion.None)
        {
            yield return StopMotion();
        }
        else
        {
            yield return Play($"incline_{heading}");
        }
        var home = _robot.World;
        var toward = (Iso.Cell(Iso.RobotOrigin, blocked.Target) - Iso.Cell(Iso.RobotOrigin, blocked.At)) * 0.12f;
        for (var t = 0f; t < 0.4f; t += Frame.Dt)
        {
            _robot.World = home + toward * MathF.Sin(t / 0.4f * MathF.PI);
            yield return null;
        }
        _robot.World = home;
        yield return 0.5f;
        yield return Play($"re-incline_{heading}");
        _robot.SetClip(heading);
        yield return Pause / 2;
    }

    private IEnumerator Reveal(QuestRevealedEvent reveal)
    {
        for (var t = 0f; t < 0.3f; t += Frame.Dt)
        {
            _questAlpha[reveal.Cell] = 1 - t / 0.3f;
            if (reveal.IsRadiation)
            {
                _radiationAlpha[reveal.Cell] = t / 0.3f;
            }
            yield return null;
        }
        _questAlpha.Remove(reveal.Cell);
        if (reveal.IsRadiation)
        {
            _radiationAlpha[reveal.Cell] = 1f;
        }
    }

    /// <summary>Возврат из наклона в обычную стойку после езды.</summary>
    private IEnumerator StopMotion()
    {
        _audio.SetMotor(false);
        if (_motion == Motion.None)
        {
            yield break;
        }
        _motion = Motion.None;
        var heading = _heading.Name();
        yield return Play($"re-incline_{heading}");
        _robot.SetClip(heading);
        yield return Pause / 2;
    }

    /// <summary>Ящики на грядках превращаются в растения (CubeController.PlantCoroutine).</summary>
    private IEnumerator Grow()
    {
        yield return 1f;
        foreach (var cube in _cubes.Values)
        {
            cube.SetClip("CubeInvisible");
        }
        yield return _assets.Animation("CubeInvisible").Length + 0.6f;
        _audio.Play(Sfx.Win);
        _audio.PauseMusic(5.5f);
        foreach (var cube in _cubes.Values)
        {
            cube.SetClip("animation_plant");
        }
        yield return _assets.Animation("animation_plant").Length;
    }

    private static float CellDepth(GridPos cell) => Iso.Step(cell.X, cell.Y).Y;

    private IEnumerator Play(string clip)
    {
        _robot.SetClip(clip);
        yield return _assets.Animation(clip).Length;
    }

    private void DrawActor(Gfx g, Actor actor, float scale)
    {
        var sprite = _assets.Animation(actor.Clip).FrameAt(actor.Time);
        g.Sprite(sprite, Iso.ToScreen(actor.World), Iso.Scale(sprite, scale));
    }

    private void DrawRotated(Gfx g, SpriteDef sprite, Vector2 world, Vector2 scale)
    {
        var s = new Vector2(Iso.Scale(sprite, scale.X), Iso.Scale(sprite, scale.Y));
        g.Batch.Draw(sprite.Texture, Iso.ToScreen(world), sprite.Source, Color.White, DecorRotation, sprite.Origin, s, SpriteEffects.None, 0f);
    }

    /// <summary>Картинка, "лежащая" на клетке (знак вопроса, радиация).</summary>
    private void DrawDecal(Gfx g, string name, GridPos cell, float size, float alpha)
    {
        var sprite = _assets.Sprite(name);
        var center = Iso.ToScreen(Iso.Cell(Iso.TileCenterOrigin, cell));
        var aspect = (float)sprite.Source.Width / sprite.Source.Height;
        var axisX = Iso.ScreenAxisY * size * aspect;
        var axisY = -Iso.ScreenAxisX * size;
        g.SpriteSkewed(sprite, center - axisX / 2 - axisY / 2, axisX, axisY, Color.White * alpha);
    }

    private enum Motion
    {
        None,
        Forward,
        Back,
    }

    private sealed class Actor(Vector2 world, string clip)
    {
        public Vector2 World { get; set; } = world;
        public string Clip { get; private set; } = clip;
        public float Time { get; set; }

        public void SetClip(string clip)
        {
            Clip = clip;
            Time = 0;
        }
    }
}

public static class DirectionNames
{
    /// <summary>Имя направления как в названиях анимаций оригинала.</summary>
    public static string Name(this Direction d) => d.ToString().ToLowerInvariant();
}
