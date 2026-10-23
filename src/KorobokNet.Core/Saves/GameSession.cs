using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Saves;

/// <summary>
/// Прогресс игроков: выбор игрока, программы и звёзды по уровням, журнал для учителя.
/// Каждое изменение сразу записывается на диск.
/// </summary>
public sealed class GameSession
{
    public const int MaxNameLength = 20;

    private readonly SaveStore _store;
    private readonly ResultLog _log;

    public GameSession(SaveStore store, ResultLog log, LevelCatalog catalog)
    {
        _store = store;
        _log = log;
        Catalog = catalog;
        Data = store.Load();
    }

    public LevelCatalog Catalog { get; }
    public SaveData Data { get; }

    public IReadOnlyList<LevelDefinition> Levels => Catalog.Levels;

    public UserProfile? User => Data.LastUser is null ? null : Data.FindUser(Data.LastUser);

    public bool SoundOff
    {
        get => Data.SoundOff;
        set
        {
            Data.SoundOff = value;
            Save();
        }
    }

    public static string NormalizeName(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public NameCheck CheckNewName(string name)
    {
        name = NormalizeName(name);
        if (name.Length == 0)
        {
            return NameCheck.Empty;
        }
        if (name.Length > MaxNameLength)
        {
            return NameCheck.TooLong;
        }
        return Data.FindUser(name) is null ? NameCheck.Ok : NameCheck.Taken;
    }

    public UserProfile CreateUser(string name)
    {
        if (CheckNewName(name) != NameCheck.Ok)
        {
            throw new ArgumentException($"Нельзя создать игрока с именем '{name}'.", nameof(name));
        }
        var user = Data.GetOrAddUser(NormalizeName(name));
        Data.LastUser = user.Name;
        Save();
        return user;
    }

    public void SelectUser(string name)
    {
        var user = Data.FindUser(name) ?? throw new ArgumentException($"Нет игрока '{name}'.", nameof(name));
        Data.LastUser = user.Name;
        Save();
    }

    public void DeleteUser(string name)
    {
        if (Data.RemoveUser(name))
        {
            _log.Delete(name);
            Save();
        }
    }

    public int? Stars(int level) => User?.Levels.GetValueOrDefault(level)?.Stars;

    public RobotProgram LoadProgram(int level)
    {
        var text = User?.Levels.GetValueOrDefault(level)?.Program;
        if (string.IsNullOrWhiteSpace(text))
        {
            return RobotProgram.Empty;
        }
        try
        {
            return RobotProgram.Parse(text);
        }
        catch (FormatException)
        {
            return RobotProgram.Empty;
        }
    }

    public void SaveProgram(int level, RobotProgram program)
    {
        if (User is not { } user)
        {
            return;
        }
        user.GetLevel(level).Program = program.ToString();
        Save();
    }

    /// <summary>Seed украшений уровня. Создаётся один раз для каждого игрока и уровня.</summary>
    public int DecorationSeed(int level)
    {
        if (User is not { } user)
        {
            return level;
        }
        var progress = user.GetLevel(level);
        if (progress.DecorationSeed is null)
        {
            progress.DecorationSeed = Random.Shared.Next();
            Save();
        }
        return progress.DecorationSeed.Value;
    }

    /// <summary>Засчитывает прохождение: звёзды (лучший результат сохраняется) и строка в журнале.</summary>
    public int RecordWin(LevelDefinition level, RobotProgram program, DateTime time)
    {
        var stats = program.Stats;
        var stars = level.Score(stats);
        if (User is { } user)
        {
            var progress = user.GetLevel(level.Number);
            progress.Stars = Math.Max(progress.Stars ?? 0, stars);
            progress.Program = program.ToString();
            Save();
            try
            {
                _log.Append(user.Name, level.Number, stats, stars, time);
            }
            catch (IOException)
            {
                // Журнал вспомогательный: ошибка записи не должна мешать игре.
            }
        }
        return stars;
    }

    public bool CompletedAll => User?.HasCompletedAll(Levels.Count) ?? false;

    private void Save() => _store.Save(Data);
}

public enum NameCheck
{
    Ok,
    Empty,
    TooLong,
    Taken,
}
