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
    public void TrashOnlyInLowerHalfTreesInUpper()
    {
        var all = Enumerable.Range(0, 50).SelectMany(DecorationGenerator.Generate).ToList();

        Assert.All(all.Where(d => d.Kind == DecorationKind.Trash), d => Assert.True(d.Cell.X - d.Cell.Y <= 0));
        Assert.All(all.Where(d => d.Kind != DecorationKind.Trash), d => Assert.True(d.Cell.X - d.Cell.Y > 0));
        Assert.Contains(all, d => d.Kind == DecorationKind.Tree1);
        Assert.Contains(all, d => d.Kind == DecorationKind.Tree2);
    }
}
