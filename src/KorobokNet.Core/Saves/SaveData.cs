namespace KorobokNet.Core.Saves;

/// <summary>Все сохранения игры: список игроков и их прогресс.</summary>
public sealed class SaveData
{
    public string? LastUser { get; set; }

    public bool SoundOff { get; set; }

    public List<UserProfile> Users { get; set; } = [];

    /// <summary>Удаляет игрока; если он был последним выбранным - сбрасывает выбор.</summary>
    public bool RemoveUser(string name)
    {
        var user = FindUser(name);
        if (user is null)
        {
            return false;
        }
        Users.Remove(user);
        if (string.Equals(LastUser, user.Name, StringComparison.OrdinalIgnoreCase))
        {
            LastUser = null;
        }
        return true;
    }

    public UserProfile? FindUser(string name) =>
        Users.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));

    public UserProfile GetOrAddUser(string name)
    {
        var user = FindUser(name);
        if (user is null)
        {
            user = new UserProfile { Name = name };
            Users.Add(user);
        }
        return user;
    }
}

public sealed class UserProfile
{
    public required string Name { get; set; }

    /// <summary>Прогресс по номеру уровня.</summary>
    public Dictionary<int, LevelProgress> Levels { get; set; } = [];

    public LevelProgress GetLevel(int number)
    {
        if (!Levels.TryGetValue(number, out var progress))
        {
            progress = new LevelProgress();
            Levels[number] = progress;
        }
        return progress;
    }

    public bool HasCompletedAll(int levelCount) =>
        Enumerable.Range(1, levelCount).All(n => Levels.TryGetValue(n, out var p) && p.Stars is not null);
}

public sealed class LevelProgress
{
    /// <summary>Лучший результат; null - уровень ещё не пройден.</summary>
    public int? Stars { get; set; }

    /// <summary>Последняя программа в текстовом виде (<see cref="Programs.RobotProgram"/>).</summary>
    public string? Program { get; set; }

    /// <summary>Seed украшений вокруг поля, чтобы картинка не менялась между запусками.</summary>
    public int? DecorationSeed { get; set; }
}
