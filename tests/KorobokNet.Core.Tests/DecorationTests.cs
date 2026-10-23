namespace KorobokNet.Core.Tests;

public class DecorationTests
{
    [Fact]
    public void SameSeedGivesSameDecorations() =>
        Assert.Equal(DecorationGenerator.Generate(42), DecorationGenerator.Generate(42));

    [Fact]
    public void DecorationsAreOutsideTheBoard()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            Assert.All(DecorationGenerator.Generate(seed), d =>
                Assert.True(d.Cell.X < -1 || d.Cell.Y < -1 || d.Cell.X > 7 || d.Cell.Y > 7, d.ToString()));
        }
    }

    [Fact]
    public void StonesStayOutOfSkyAndTabletButton()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var stones = DecorationGenerator.Generate(seed);

            Assert.InRange(stones.Count, 6, DecorationGenerator.MaxCount);
            Assert.All(stones, d => Assert.True(DecorationGenerator.Allowed(d.Cell.X, d.Cell.Y), d.ToString()));
            Assert.All(stones, a => Assert.Single(stones, b => DecorationGenerator.Touches(a.Cell, b.Cell)));
        }
        Assert.False(DecorationGenerator.Allowed(10, 0));
        Assert.False(DecorationGenerator.Allowed(9, 4));
        Assert.False(DecorationGenerator.Allowed(4, 10));
    }
}
