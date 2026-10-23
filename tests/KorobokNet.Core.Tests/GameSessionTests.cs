using KorobokNet.Core.Programs;
using KorobokNet.Core.Saves;

namespace KorobokNet.Core.Tests;

public sealed class GameSessionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KorobokSession_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private GameSession NewSession() =>
        new(new SaveStore(Path.Combine(_dir, "save.json")), new ResultLog(Path.Combine(_dir, "results")), LevelLoader.LoadCatalog());

    [Theory]
    [InlineData("", NameCheck.Empty)]
    [InlineData("   ", NameCheck.Empty)]
    [InlineData("Маша", NameCheck.Ok)]
    [InlineData("Очень длинное имя игрока", NameCheck.TooLong)]
    public void ChecksNames(string name, NameCheck expected) => Assert.Equal(expected, NewSession().CheckNewName(name));

    [Fact]
    public void CreatedUserIsSelectedAndPersisted()
    {
        NewSession().CreateUser("  Маша   Петрова ");

        var session = NewSession();

        Assert.Equal("Маша Петрова", session.User?.Name);
        Assert.Equal(NameCheck.Taken, session.CheckNewName("маша петрова"));
    }

    [Fact]
    public void WinKeepsBestStarsAndWritesLog()
    {
        var session = NewSession();
        session.CreateUser("Петя");
        var level = session.Levels[0];

        Assert.Equal(1, session.RecordWin(level, RobotProgram.Parse("L R R R F"), DateTime.Now));
        Assert.Equal(3, session.RecordWin(level, RobotProgram.Parse("L F"), DateTime.Now));
        Assert.Equal(1, session.RecordWin(level, RobotProgram.Parse("L R R R F"), DateTime.Now));

        Assert.Equal(3, NewSession().Stars(1));
        Assert.Equal(3, File.ReadAllLines(Path.Combine(_dir, "results", "Петя.txt")).Length);
    }

    [Fact]
    public void ProgramAndDecorationSeedArePersistedPerLevel()
    {
        var session = NewSession();
        session.CreateUser("Оля");
        session.SaveProgram(5, RobotProgram.Parse("?(L|F)"));
        var seed = session.DecorationSeed(5);

        var reloaded = NewSession();

        Assert.Equal("?(L|F)", reloaded.LoadProgram(5).ToString());
        Assert.Equal(RobotProgram.Empty.ToString(), reloaded.LoadProgram(6).ToString());
        Assert.Equal(seed, reloaded.DecorationSeed(5));
    }

    [Fact]
    public void DeleteUserRemovesProgressAndLog()
    {
        var session = NewSession();
        session.CreateUser("Вася");
        session.RecordWin(session.Levels[0], RobotProgram.Parse("L F"), DateTime.Now);

        session.DeleteUser("вася");

        Assert.Null(session.User);
        Assert.Empty(NewSession().Data.Users);
        Assert.False(File.Exists(Path.Combine(_dir, "results", "Вася.txt")));
    }

    [Fact]
    public void BrokenSavedProgramLoadsAsEmpty()
    {
        var session = NewSession();
        session.CreateUser("Коля");
        session.User!.GetLevel(2).Program = "F (((";

        Assert.Empty(session.LoadProgram(2).Instructions);
    }
}
