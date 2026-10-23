namespace KorobokNet.Core;

/// <summary>Что произошло на поле. Отрисовка проигрывает эти события как анимации.</summary>
public abstract record GameEvent;

public sealed record TurnedEvent(Direction From, Direction To, bool ToRight) : GameEvent;

/// <summary>Робот переехал. <see cref="Push"/> задан, если он толкнул ящик.</summary>
public sealed record MovedEvent(GridPos From, GridPos To, bool Backward, CubePush? Push) : GameEvent;

public sealed record CubePush(GridPos From, GridPos To);

/// <summary>Робот упёрся: путь закрыт, он остался на месте.</summary>
public sealed record BlockedEvent(GridPos At, GridPos Target, bool Backward) : GameEvent;

/// <summary>Робот посмотрел на клетку "?" и узнал, что в ней.</summary>
public sealed record QuestRevealedEvent(GridPos Cell, bool IsRadiation) : GameEvent;

public sealed record LevelWonEvent : GameEvent;
