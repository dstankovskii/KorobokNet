using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Saves;

/// <summary>
/// Прогресс игроков: выбор игрока, программы и звёзды по уровням.
/// Каждое изменение сразу записывается на диск.
/// </summary>
public sealed class GameSession
{
    public const int MaxNameLength = 20;

    private readonly SaveStore _store;

    public GameSession(SaveStore store, LevelCatalog catalog)
    {
        _store = store;
        Catalog = catalog;
        Data = store.Load();
    }

    public LevelCatalog Catalog { get; }
    public SaveData Data { get; }

    public IReadOnlyList<LevelDefinition> Levels => Catalog.Levels;

    public UserProfile? User => Data.LastUser is null ? null : Data.FindUser(Data.LastUser);

    public bool MusicOff
    {
        get => Data.MusicOff;
        set
        {
            Data.MusicOff = value;
            Save();
        }
    }

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
            Save();
        }
    }

    public int? Stars(int level) => User?.Levels.GetValueOrDefault(level)?.Stars;

    /// <summary>Уровень открыт, если пройден предыдущий. Первый открыт всегда.</summary>
    public bool IsUnlocked(int level) => level <= 1 || Stars(level - 1) is not null;

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

    /// <summary>Засчитывает прохождение. Сохраняется лучший результат.</summary>
    public int RecordWin(LevelDefinition level, RobotProgram program)
    {
        var stars = level.Score(program.Stats);
        if (User is { } user)
        {
            var progress = user.GetLevel(level.Number);
            progress.Stars = Math.Max(progress.Stars ?? 0, stars);
            progress.Program = program.ToString();
            Save();
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
