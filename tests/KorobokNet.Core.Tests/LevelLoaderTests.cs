namespace KorobokNet.Core.Tests;

public class LevelLoaderTests
{
    [Fact]
    public void LoadsTwelveLevels()
    {
        var levels = LevelLoader.LoadBuiltIn();

        Assert.Equal(12, levels.Count);
        Assert.Equal(Enumerable.Range(1, 12), levels.Select(l => l.Number));
    }

    [Fact]
    public void FirstLevelMatchesOriginalLayout()
    {
        var level = LevelLoader.LoadBuiltIn()[0];

        Assert.Equal(Direction.North, level.StartHeading);
        Assert.Equal(new GridPos(3, 3), level.Robot);
        Assert.Equal([new GridPos(3, 4)], level.Cubes);
        Assert.Equal([new GridPos(3, 5)], level.Soil);
        Assert.Empty(level.Quests);
    }

    [Fact]
    public void TutorialSolutionWins()
    {
        var tutorial = LevelLoader.LoadCatalog().Tutorial;
        Assert.NotNull(tutorial.Solution);
        var board = new Board(tutorial, []);

        var runner = new ProgramRunner(board, tutorial.Solution);
        runner.RunToEnd();

        Assert.Equal(0, tutorial.Number);
        Assert.True(runner.IsWon);
    }

    [Fact]
    public void LevelsWithConditionsHaveQuestCells()
    {
        var levels = LevelLoader.LoadBuiltIn();

        Assert.All(levels.Where(l => l.Number is >= 5 and <= 8), l => Assert.Equal(2, l.Quests.Count));
        Assert.All(levels.Where(l => l.Number is < 5 or > 8), l => Assert.Empty(l.Quests));
    }

    [Theory]
    [InlineData("""{"levels":[{"heading":"north","rows":["000000"],"stars":{"three":{},"two":{}}}]}""")]
    [InlineData("""{"levels":[{"heading":"north","rows":["0c0000","0s0000","000000","000000","000000","000000"],"stars":{"three":{},"two":{}}}]}""")]
    [InlineData("""{"levels":[{"heading":"north","rows":["kc0000","0s0000","000000","000000","000000","00000x"],"stars":{"three":{},"two":{}}}]}""")]
    [InlineData("""{"levels":[{"heading":"up","rows":["kc0000","0s0000","000000","000000","000000","000000"],"stars":{"three":{},"two":{}}}]}""")]
    public void RejectsBrokenLevels(string json) => Assert.Throws<FormatException>(() => LevelLoader.Parse(json));
}
