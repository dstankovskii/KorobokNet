using KorobokNet.Core.Editing;
using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Tests;

public class ProgramLayoutTests
{
    private static (ProgramEditor Editor, ProgramLayout Layout) Build(string program)
    {
        var editor = ProgramEditor.FromProgram(RobotProgram.Parse(program));
        return (editor, ProgramLayout.Build(editor));
    }

    private static string Describe(ProgramLayout layout) =>
        string.Join(" ", layout.Blocks.OrderBy(b => b.Row).ThenBy(b => b.Column).Select(b => $"{b.Kind}@{b.Column},{b.Row}"));

    [Fact]
    public void EmptyProgramHasOnlyStart()
    {
        var (_, layout) = Build("");

        Assert.Equal("Start@0,0", Describe(layout));
        Assert.Equal(1, layout.Rows);
        Assert.Empty(layout.Links);
    }

    [Fact]
    public void LinearProgramIsOneRow()
    {
        var (_, layout) = Build("F L R B");

        Assert.Equal("Start@0,0 Forward@1,0 TurnLeft@2,0 TurnRight@3,0 Back@4,0", Describe(layout));
        Assert.Equal(5, layout.Columns);
        Assert.Equal(4, layout.Links.Count);
    }

    [Fact]
    public void LoopWrapsBody()
    {
        var (_, layout) = Build("3(F R) B");

        Assert.Equal("Start@0,0 LoopStart@1,0 Forward@2,0 TurnRight@3,0 LoopEnd@4,0 Back@5,0", Describe(layout));
    }

    [Fact]
    public void IfForksIntoYesAndNoRows()
    {
        var (_, layout) = Build("?(F F|L) B");

        Assert.Equal(
            "Start@0,0 IfStart@1,0 IfThen@2,0 Forward@3,0 Forward@4,0 IfEnd@5,0 Back@6,0 IfElse@2,1 TurnLeft@3,1",
            Describe(layout));
        Assert.Equal(2, layout.Rows);
        Assert.Contains(new LayoutLink(1, 0, 2, 0), layout.Links);
        Assert.Contains(new LayoutLink(1, 0, 1, 1), layout.Links);
        Assert.Contains(new LayoutLink(1, 1, 2, 1), layout.Links);
        Assert.Contains(new LayoutLink(3, 1, 5, 1), layout.Links);
        Assert.Contains(new LayoutLink(5, 1, 5, 0), layout.Links);
    }

    [Fact]
    public void EmptyIfStillHasBothBranches()
    {
        var (_, layout) = Build("?(|)");

        Assert.Equal("Start@0,0 IfStart@1,0 IfThen@2,0 IfEnd@3,0 IfElse@2,1", Describe(layout));
    }

    [Fact]
    public void ConditionBlockHasNoCursorSlot()
    {
        var (_, layout) = Build("?(F|L)");

        Assert.Null(Assert.Single(layout.Blocks, b => b.Kind == BlockKind.IfStart).Slot);
    }

    [Fact]
    public void EverySlotHasExactlyOneBlock()
    {
        var (editor, layout) = Build("F ?(L 2(F)|R) 3(B ?(F|))");

        var slots = editor.AllSlots();

        Assert.Equal(slots.Count, layout.Blocks.Count(b => b.Slot is not null));
        foreach (var slot in slots)
        {
            Assert.Single(layout.Blocks, b => b.Holds(slot));
        }
    }

    [Fact]
    public void CursorBlockFollowsEditor()
    {
        var (editor, _) = Build("F L");
        editor.MoveLeft();

        var layout = ProgramLayout.Build(editor);

        Assert.Equal(BlockKind.Forward, layout.CursorBlock(editor).Kind);
    }
}
