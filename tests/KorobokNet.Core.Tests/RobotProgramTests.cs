using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Tests;

public class RobotProgramTests
{
    [Theory]
    [InlineData("")]
    [InlineData("F B L R")]
    [InlineData("3(F R) F")]
    [InlineData("?(L F|F F) 2(?(R|F))")]
    [InlineData("?(|F)")]
    public void FormatRoundTrips(string text) => Assert.Equal(text, RobotProgram.Parse(text).ToString());

    [Fact]
    public void ParseIgnoresSpacingAndCase() =>
        Assert.Equal("F L 2(F R)", RobotProgram.Parse(" f l2( f r ) ").ToString());

    [Fact]
    public void ParseBuildsTree()
    {
        var program = RobotProgram.Parse("?(L|F) 2(B)");

        var iff = Assert.IsType<IfRadiationInstruction>(program.Instructions[0]);
        Assert.Equal(new CommandInstruction(Command.TurnLeft), Assert.Single(iff.Then));
        Assert.Equal(new CommandInstruction(Command.Forward), Assert.Single(iff.Else));
        var loop = Assert.IsType<RepeatInstruction>(program.Instructions[1]);
        Assert.Equal(2, loop.Count);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("F)")]
    [InlineData("3(F")]
    [InlineData("?(F)")]
    [InlineData("0(F)")]
    [InlineData("3 F")]
    public void ParseRejectsInvalid(string text) => Assert.Throws<FormatException>(() => RobotProgram.Parse(text));

    [Fact]
    public void StatsCountProgramTextNotExecution()
    {
        var stats = RobotProgram.Parse("F 5(F R) ?(L F|F) 2(?(B|R))").Stats;

        Assert.Equal(new ProgramStats(Actions: 8, Ifs: 2, Loops: 2), stats);
    }
}
