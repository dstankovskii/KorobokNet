using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Tests;

/// <summary>Пороги звёзд совпадают с оригинальным ProgramKorob.Count().</summary>
public class ScoringTests
{
    private static int Score(int level, int actions, int ifs = 0, int loops = 0) =>
        LevelLoader.LoadBuiltIn()[level - 1].Score(new ProgramStats(actions, ifs, loops));

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(1, 3, 2)]
    [InlineData(1, 4, 1)]
    [InlineData(3, 9, 3)]
    [InlineData(3, 11, 2)]
    [InlineData(3, 12, 1)]
    public void ActionLevels(int level, int actions, int stars) => Assert.Equal(stars, Score(level, actions));

    [Theory]
    [InlineData(12, 1, 3)]
    [InlineData(12, 3, 1)]
    [InlineData(8, 1, 1)]
    [InlineData(9, 1, 3)]
    [InlineData(13, 1, 2)]
    [InlineData(14, 2, 2)]
    [InlineData(15, 1, 1)]
    [InlineData(10, 0, 1)]
    public void ConditionLevel5(int actions, int ifs, int stars) => Assert.Equal(stars, Score(5, actions, ifs));

    [Theory]
    [InlineData(6, 1, 3)]
    [InlineData(6, 2, 3)]
    [InlineData(6, 3, 2)]
    [InlineData(8, 1, 2)]
    [InlineData(9, 1, 1)]
    [InlineData(4, 0, 1)]
    public void LoopLevel9(int actions, int loops, int stars) => Assert.Equal(stars, Score(9, actions, loops: loops));
}
