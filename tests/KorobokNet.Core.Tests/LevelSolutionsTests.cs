using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Tests;

/// <summary>Каждый уровень проходим, и эталонное решение получает 3 звезды при любой раскладке радиации.</summary>
public class LevelSolutionsTests
{
    public static TheoryData<int> LevelNumbers => [.. Enumerable.Range(1, 12)];

    [Theory]
    [MemberData(nameof(LevelNumbers))]
    public void SolutionWinsWithThreeStarsForEveryRadiationLayout(int number)
    {
        var level = LevelLoader.LoadBuiltIn()[number - 1];
        Assert.NotNull(level.Solution);

        foreach (var radiation in RadiationLayouts(level))
        {
            var board = new Board(level, radiation);
            var runner = new ProgramRunner(board, level.Solution);
            runner.RunToEnd();

            Assert.True(runner.IsWon, $"Уровень {number}, радиация [{string.Join(", ", radiation)}]: {level.Solution}");
        }
        Assert.Equal(3, level.Score(level.Solution.Stats));
    }

    [Fact]
    public void ConditionAndLoopLevelsRequireThoseConstructs()
    {
        var levels = LevelLoader.LoadBuiltIn();

        Assert.All(levels.Where(l => l.Number is >= 5 and <= 8),
            l => Assert.True(l.Solution!.Stats.Ifs > 0, $"Уровень {l.Number}"));
        Assert.All(levels.Where(l => l.Number >= 9),
            l => Assert.True(l.Solution!.Stats.Loops > 0, $"Уровень {l.Number}"));
    }

    private static IEnumerable<IReadOnlyList<GridPos>> RadiationLayouts(LevelDefinition level) =>
        Combinations(level.Quests, level.Quests.Count / 2);

    private static IEnumerable<IReadOnlyList<GridPos>> Combinations(IReadOnlyList<GridPos> items, int k, int start = 0)
    {
        if (k == 0)
        {
            yield return [];
            yield break;
        }
        for (var i = start; i <= items.Count - k; i++)
        {
            foreach (var rest in Combinations(items, k - 1, i + 1))
            {
                yield return [items[i], .. rest];
            }
        }
    }
}
