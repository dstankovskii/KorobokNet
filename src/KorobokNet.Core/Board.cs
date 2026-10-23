namespace KorobokNet.Core;

/// <summary>
/// Состояние поля во время прохождения уровня. Правила из оригинальных KorobController,
/// CubeController и MapScript, но пошагово, без анимаций.
/// </summary>
public sealed class Board
{
    private readonly HashSet<GridPos> _cubes;
    private readonly HashSet<GridPos> _soil;
    private readonly HashSet<GridPos> _trash;
    private readonly HashSet<GridPos> _hiddenQuests;
    private readonly HashSet<GridPos> _radiation;

    public Board(LevelDefinition level, IEnumerable<GridPos> radiation)
    {
        Level = level;
        Robot = level.Robot;
        Heading = level.StartHeading;
        _cubes = [.. level.Cubes];
        _soil = [.. level.Soil];
        _trash = [.. level.Trash];
        _hiddenQuests = [.. level.Quests];
        _radiation = [.. radiation];
        if (!_radiation.IsSubsetOf(level.Quests))
        {
            throw new ArgumentException("Радиация может быть только в клетках «?».", nameof(radiation));
        }
    }

    /// <summary>Поле со случайной радиацией в половине клеток "?".</summary>
    public static Board Create(LevelDefinition level, Random random) =>
        new(level, PickRadiation(level, random));

    public static IReadOnlyList<GridPos> PickRadiation(LevelDefinition level, Random random) =>
        level.Quests.OrderBy(_ => random.Next()).Take(level.Quests.Count / 2).ToList();

    public LevelDefinition Level { get; }
    public GridPos Robot { get; private set; }
    public Direction Heading { get; private set; }
    public IReadOnlyCollection<GridPos> Cubes => _cubes;
    public IReadOnlyCollection<GridPos> Radiation => _radiation;
    public IReadOnlyCollection<GridPos> HiddenQuests => _hiddenQuests;

    public GridPos Ahead => Robot + Heading.Offset();
    public GridPos Behind => Robot - Heading.Offset();

    public bool IsRadiationAhead => _radiation.Contains(Ahead);

    public bool IsWon => _cubes.Count > 0 && _cubes.All(_soil.Contains);

    public bool IsQuestRevealed(GridPos cell) => Level.Quests.Contains(cell) && !_hiddenQuests.Contains(cell);

    /// <summary>
    /// Непроходимо для робота и ящика: рамка, мусор, нераскрытый "?" и радиация.
    /// </summary>
    public bool IsSolid(GridPos p) =>
        LevelDefinition.IsBorder(p) || _trash.Contains(p) || _hiddenQuests.Contains(p) || _radiation.Contains(p);

    /// <summary>Робот смотрит на клетку перед собой: "?" раскрывается.</summary>
    public QuestRevealedEvent? RevealAhead()
    {
        var cell = Ahead;
        if (!_hiddenQuests.Remove(cell))
        {
            return null;
        }
        return new QuestRevealedEvent(cell, _radiation.Contains(cell));
    }

    public TurnedEvent TurnRight()
    {
        var from = Heading;
        Heading = Heading.TurnRight();
        return new TurnedEvent(from, Heading, ToRight: true);
    }

    public TurnedEvent TurnLeft()
    {
        var from = Heading;
        Heading = Heading.TurnLeft();
        return new TurnedEvent(from, Heading, ToRight: false);
    }

    /// <summary>Шаг вперёд. Ящик впереди толкается, если за ним свободно.</summary>
    public GameEvent Forward()
    {
        var target = Ahead;
        if (IsSolid(target))
        {
            return new BlockedEvent(Robot, target, Backward: false);
        }

        CubePush? push = null;
        if (_cubes.Contains(target))
        {
            var beyond = target + Heading.Offset();
            if (IsSolid(beyond) || _cubes.Contains(beyond))
            {
                return new BlockedEvent(Robot, target, Backward: false);
            }
            _cubes.Remove(target);
            _cubes.Add(beyond);
            push = new CubePush(target, beyond);
        }

        var from = Robot;
        Robot = target;
        return new MovedEvent(from, target, Backward: false, push);
    }

    /// <summary>
    /// Шаг назад. Ящик назад не тянется и не толкается, на грядку задом заехать нельзя.
    /// </summary>
    public GameEvent Back()
    {
        var target = Behind;
        if (IsSolid(target) || _cubes.Contains(target) || _soil.Contains(target))
        {
            return new BlockedEvent(Robot, target, Backward: true);
        }

        var from = Robot;
        Robot = target;
        return new MovedEvent(from, target, Backward: true, Push: null);
    }
}
