using static KorobokNet.Core.Tests.TestLevels;

namespace KorobokNet.Core.Tests;

public class BoardTests
{
    private const string Empty = "000000";
    private const string CubeAndSoil = "cs0000";

    [Fact]
    public void ForwardMovesOneCell()
    {
        var board = new Board(Make("west", "k00000", Empty, Empty, Empty, Empty, CubeAndSoil), []);

        var moved = Assert.IsType<MovedEvent>(board.Forward());

        Assert.Equal(Cell(0, 1), board.Robot);
        Assert.Equal(new MovedEvent(Cell(0, 0), Cell(0, 1), Backward: false, Push: null), moved);
    }

    [Fact]
    public void ForwardIntoTrashIsBlocked()
    {
        var board = new Board(Make("west", "kt0000", Empty, Empty, Empty, Empty, CubeAndSoil), []);

        Assert.IsType<BlockedEvent>(board.Forward());
        Assert.Equal(Cell(0, 0), board.Robot);
    }

    [Fact]
    public void ForwardIntoBorderIsBlocked()
    {
        var board = new Board(Make("west", "00000k", Empty, Empty, Empty, Empty, CubeAndSoil), []);

        Assert.IsType<BlockedEvent>(board.Forward());
        Assert.Equal(Cell(0, 5), board.Robot);
    }

    [Fact]
    public void ForwardPushesCubeOntoSoilAndWins()
    {
        var board = new Board(Make("west", "kcs000", Empty, Empty, Empty, Empty, Empty), []);

        var moved = Assert.IsType<MovedEvent>(board.Forward());

        Assert.Equal(new CubePush(Cell(0, 1), Cell(0, 2)), moved.Push);
        Assert.Equal([Cell(0, 2)], board.Cubes);
        Assert.True(board.IsWon);
    }

    [Theory]
    [InlineData("kct000")]
    [InlineData("kcc000")]
    [InlineData("0000kc")]
    public void PushIntoObstacleIsBlocked(string row)
    {
        var board = new Board(Make("west", row, "ss0000", Empty, Empty, Empty, Empty), []);
        var robot = board.Robot;
        var cubes = board.Cubes.ToHashSet();

        Assert.IsType<BlockedEvent>(board.Forward());
        Assert.Equal(robot, board.Robot);
        Assert.Equal(cubes, board.Cubes.ToHashSet());
    }

    [Fact]
    public void PushingCubeOffSoilUndoesWin()
    {
        var board = new Board(Make("west", "kcs000", Empty, Empty, Empty, Empty, Empty), []);

        board.Forward();
        Assert.True(board.IsWon);
        Assert.IsType<MovedEvent>(board.Forward());
        Assert.False(board.IsWon);
    }

    [Fact]
    public void BackMovesWithoutPushing()
    {
        var board = new Board(Make("west", "0k0000", Empty, Empty, Empty, Empty, CubeAndSoil), []);

        var moved = Assert.IsType<MovedEvent>(board.Back());

        Assert.True(moved.Backward);
        Assert.Equal(Cell(0, 0), board.Robot);
        Assert.Equal(Direction.West, board.Heading);
    }

    [Theory]
    [InlineData("ck0000", "s00000")]
    [InlineData("sk0000", "cs0000")]
    [InlineData("tk0000", CubeAndSoil)]
    [InlineData("?k0000", CubeAndSoil)]
    public void BackIntoCubeSoilTrashOrHiddenCellIsBlocked(string row, string other)
    {
        var board = new Board(Make("west", row, other, Empty, Empty, Empty, Empty), []);

        Assert.IsType<BlockedEvent>(board.Back());
        Assert.Equal(Cell(0, 1), board.Robot);
    }

    [Fact]
    public void HiddenQuestBlocksUntilRevealed()
    {
        var board = new Board(Make("west", "k?0000", Empty, Empty, Empty, Empty, CubeAndSoil), []);

        Assert.IsType<BlockedEvent>(board.Forward());
        Assert.Equal(new QuestRevealedEvent(Cell(0, 1), IsRadiation: false), board.RevealAhead());
        Assert.Null(board.RevealAhead());
        Assert.IsType<MovedEvent>(board.Forward());
    }

    [Fact]
    public void RadiationStaysBlockedAfterReveal()
    {
        var level = Make("west", "k?0000", Empty, Empty, Empty, Empty, CubeAndSoil);
        var board = new Board(level, [Cell(0, 1)]);

        Assert.True(board.IsRadiationAhead);
        Assert.Equal(new QuestRevealedEvent(Cell(0, 1), IsRadiation: true), board.RevealAhead());
        Assert.IsType<BlockedEvent>(board.Forward());
    }

    [Fact]
    public void CubeCannotBePushedIntoHiddenQuest()
    {
        var board = new Board(Make("west", "kc?000", "s00000", Empty, Empty, Empty, Empty), []);

        Assert.IsType<BlockedEvent>(board.Forward());
    }

    [Fact]
    public void RadiationOnlyInQuestCells()
    {
        var level = Make("west", "k?0000", Empty, Empty, Empty, Empty, CubeAndSoil);

        Assert.Throws<ArgumentException>(() => new Board(level, [Cell(0, 2)]));
    }

    [Fact]
    public void RandomRadiationCoversHalfOfQuests()
    {
        var level = Make("west", "k?0?00", "0?0?00", Empty, Empty, Empty, CubeAndSoil);

        for (var seed = 0; seed < 20; seed++)
        {
            var radiation = Board.PickRadiation(level, new Random(seed));
            Assert.Equal(2, radiation.Distinct().Count());
            Assert.All(radiation, r => Assert.Contains(r, level.Quests));
        }
    }
}
