namespace KorobokNet.Core.Tests;

public class DirectionTests
{
    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void LeftUndoesRight(Direction d) => Assert.Equal(d, d.TurnRight().TurnLeft());

    [Fact]
    public void FourRightTurnsMakeFullCircle()
    {
        Assert.Equal(Direction.North, Direction.West.TurnRight());
        Assert.Equal(Direction.West, Direction.West.TurnRight().TurnRight().TurnRight().TurnRight());
    }

    [Fact]
    public void OppositeDirectionsHaveOppositeOffsets()
    {
        Assert.Equal(new GridPos(0, 0), Direction.North.Offset() + Direction.South.Offset());
        Assert.Equal(new GridPos(0, 0), Direction.West.Offset() + Direction.East.Offset());
    }

    [Fact]
    public void ParseRejectsUnknown() => Assert.Throws<FormatException>(() => DirectionExtensions.Parse("up"));
}
