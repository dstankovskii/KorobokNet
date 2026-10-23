using KorobokNet.Core.Programs;
using KorobokNet.Core.Saves;

namespace KorobokNet.Core.Tests;

public sealed class SaveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KorobokTests_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void MissingFileGivesEmptySave() =>
        Assert.Empty(new SaveStore(Path.Combine(_dir, "save.json")).Load().Users);

    [Fact]
    public void SaveRoundTrips()
    {
        var store = new SaveStore(Path.Combine(_dir, "nested", "save.json"));
        var data = new SaveData { LastUser = "Маша" };
        var level = data.GetOrAddUser("Маша").GetLevel(5);
        level.Stars = 3;
        level.Program = "?(L|F F)";
        level.DecorationSeed = 123;

        store.Save(data);
        var loaded = store.Load();

        Assert.Equal("Маша", loaded.LastUser);
        var user = loaded.FindUser("маша");
        Assert.NotNull(user);
        var progress = user.Levels[5];
        Assert.Equal(3, progress.Stars);
        Assert.Equal("?(L|F F)", progress.Program);
        Assert.Equal(123, progress.DecorationSeed);
    }

    [Fact]
    public void GetOrAddUserDoesNotDuplicate()
    {
        var data = new SaveData();

        data.GetOrAddUser("Петя");
        data.GetOrAddUser("ПЕТЯ");

        Assert.Single(data.Users);
    }

    [Fact]
    public void CompletedAllRequiresStarsOnEveryLevel()
    {
        var user = new UserProfile { Name = "Оля" };
        for (var n = 1; n <= 11; n++)
        {
            user.GetLevel(n).Stars = 1;
        }
        user.GetLevel(12).Program = "F";

        Assert.False(user.HasCompletedAll(12));
        user.GetLevel(12).Stars = 2;
        Assert.True(user.HasCompletedAll(12));
    }

    [Fact]
    public void ResultLogLineMatchesOriginalWording() =>
        Assert.Equal(
            "Уровень: 5 | действий: 12 | циклов: 0 | условий: 1 | баллы: 3 | время: 22.10.2026 09:05",
            ResultLog.FormatLine(5, new ProgramStats(12, 1, 0), 3, new DateTime(2026, 10, 22, 9, 5, 0)));

    [Fact]
    public void ResultLogAppendsPerUserFile()
    {
        var log = new ResultLog(_dir);

        log.Append("Вася/Пупкин", 1, new ProgramStats(2, 0, 0), 3, DateTime.Now);
        log.Append("Вася/Пупкин", 2, new ProgramStats(4, 0, 0), 3, DateTime.Now);

        var path = log.FilePathFor("Вася/Пупкин");
        Assert.StartsWith(_dir, path);
        Assert.Equal(2, File.ReadAllLines(path).Length);
    }
}
