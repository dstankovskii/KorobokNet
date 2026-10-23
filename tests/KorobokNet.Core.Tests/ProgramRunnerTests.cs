using KorobokNet.Core.Programs;
using static KorobokNet.Core.Tests.TestLevels;

namespace KorobokNet.Core.Tests;

public class ProgramRunnerTests
{
    private const string Empty = "000000";
    private const string CubeAndSoil = "cs0000";

    private static (Board Board, ProgramRunner Runner) Run(LevelDefinition level, string program, params GridPos[] radiation)
    {
        var board = new Board(level, radiation);
        var runner = new ProgramRunner(board, RobotProgram.Parse(program));
        runner.RunToEnd();
        return (board, runner);
    }

    [Fact]
    public void RevealsCellAheadBeforeFirstStep()
    {
        var board = new Board(Make("west", "k?0000", Empty, Empty, Empty, Empty, CubeAndSoil), []);

        var runner = new ProgramRunner(board, RobotProgram.Parse("F"));

        Assert.Equal([new QuestRevealedEvent(Cell(0, 1), IsRadiation: false)], runner.InitialEvents);
    }

    [Fact]
    public void RevealsCellAheadAfterTurn()
    {
        var board = new Board(Make("west", "k00000", "?00000", Empty, Empty, Empty, CubeAndSoil), []);
        var runner = new ProgramRunner(board, RobotProgram.Parse("R"));

        Assert.Empty(runner.InitialEvents);
        var events = runner.Step();

        Assert.IsType<TurnedEvent>(events[0]);
        Assert.Equal(new QuestRevealedEvent(Cell(1, 0), IsRadiation: false), events[1]);
    }

    [Fact]
    public void RepeatRunsBodyCountTimes()
    {
        var (board, runner) = Run(Make("west", "k00000", Empty, Empty, Empty, Empty, CubeAndSoil), "3(F)");

        Assert.Equal(Cell(0, 3), board.Robot);
        Assert.Equal(3, runner.StepsTaken);
        Assert.True(runner.IsFinished);
        Assert.False(runner.IsWon);
    }

    [Theory]
    [InlineData(true, Direction.South)]
    [InlineData(false, Direction.North)]
    public void IfChoosesBranchByRadiationAhead(bool radiation, Direction expected)
    {
        var level = Make("west", "k?0000", Empty, Empty, Empty, Empty, CubeAndSoil);

        var (board, _) = radiation ? Run(level, "?(L|R)", Cell(0, 1)) : Run(level, "?(L|R)");

        Assert.Equal(expected, board.Heading);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IfReportsWhatRobotSaw(bool radiation)
    {
        var board = new Board(Make("west", "k?0000", Empty, Empty, Empty, Empty, CubeAndSoil), radiation ? [Cell(0, 1)] : []);
        var runner = new ProgramRunner(board, RobotProgram.Parse("?(L|R)"));

        var events = runner.Step();

        Assert.Equal(new ConditionCheckedEvent(Cell(0, 1), radiation), events[0]);
        Assert.IsType<TurnedEvent>(events[1]);
    }

    [Fact]
    public void IfInsideLoopIsCheckedOnEveryIteration()
    {
        var level = Make("west", "k?0?00", Empty, Empty, Empty, Empty, CubeAndSoil);

        var (board, runner) = Run(level, "2(?(L|F F))", Cell(0, 3));

        Assert.Equal(Cell(0, 2), board.Robot);
        Assert.Equal(Direction.South, board.Heading);
        Assert.Equal(3, runner.StepsTaken);
    }

    [Fact]
    public void StopsAsSoonAsLevelIsWon()
    {
        var (board, runner) = Run(Make("west", "kcs000", Empty, Empty, Empty, Empty, Empty), "F F F");

        Assert.True(runner.IsWon);
        Assert.Equal(1, runner.StepsTaken);
        Assert.Equal(Cell(0, 1), board.Robot);
    }

    [Fact]
    public void WinIsReportedAsLastEvent()
    {
        var board = new Board(Make("west", "kcs000", Empty, Empty, Empty, Empty, Empty), []);
        var runner = new ProgramRunner(board, RobotProgram.Parse("F"));

        var events = runner.Step();

        Assert.IsType<MovedEvent>(events[0]);
        Assert.IsType<LevelWonEvent>(events[^1]);
        Assert.True(runner.IsFinished);
    }

    [Fact]
    public void BlockedStepsDoNotStopProgram()
    {
        var (board, runner) = Run(Make("west", "kt0000", Empty, Empty, Empty, Empty, CubeAndSoil), "F F R F");

        Assert.Equal(4, runner.StepsTaken);
        Assert.Equal(Cell(1, 0), board.Robot);
    }

    [Fact]
    public void EmptyProgramFinishesImmediately()
    {
        var board = new Board(Make("west", "k00000", Empty, Empty, Empty, Empty, CubeAndSoil), []);
        var runner = new ProgramRunner(board, RobotProgram.Empty);

        Assert.Empty(runner.Step());
        Assert.True(runner.IsFinished);
    }
}
