using KorobokNet.Core.Editing;
using KorobokNet.Core.Programs;

namespace KorobokNet.Core.Tests;

public class ProgramEditorTests
{
    private static ProgramEditor Editor(string program) => ProgramEditor.FromProgram(RobotProgram.Parse(program));

    private static string Text(ProgramEditor editor) => editor.ToProgram().ToString();

    [Theory]
    [InlineData("")]
    [InlineData("F L R B")]
    [InlineData("?(L F|F F) 3(F R)")]
    [InlineData("2(?(F|L))")]
    public void FromProgramRoundTrips(string program) => Assert.Equal(program, Text(Editor(program)));

    [Fact]
    public void InsertsAtCursorAndAdvances()
    {
        var editor = new ProgramEditor();

        editor.InsertCommand(Command.Forward);
        editor.InsertCommand(Command.TurnLeft);
        editor.MoveLeft();
        editor.InsertCommand(Command.Back);

        Assert.Equal("F B L", Text(editor));
    }

    [Fact]
    public void IfPutsCursorIntoYesBranch()
    {
        var editor = new ProgramEditor();

        editor.InsertIf();
        editor.InsertCommand(Command.TurnLeft);
        editor.MoveRight();
        editor.InsertCommand(Command.Forward);
        editor.MoveRight();
        editor.InsertCommand(Command.Back);

        Assert.Equal("?(L|F) B", Text(editor));
    }

    [Fact]
    public void LoopPutsCursorIntoBody()
    {
        var editor = new ProgramEditor();

        editor.InsertLoop(3);
        editor.InsertCommand(Command.Forward);
        editor.InsertCommand(Command.TurnRight);
        editor.MoveRight();
        editor.InsertCommand(Command.Back);

        Assert.Equal("3(F R) B", Text(editor));
    }

    [Fact]
    public void NoIfInsideIfAndNoLoopInsideLoop()
    {
        var editor = new ProgramEditor();
        editor.InsertIf();

        Assert.False(editor.InsertIf());
        Assert.True(editor.InsertLoop());
        Assert.False(editor.InsertLoop());
        Assert.False(editor.CanInsertIf);
        Assert.Equal("?(2()|)", Text(editor));
    }

    [Fact]
    public void LoopCountIsClamped()
    {
        var editor = new ProgramEditor();
        editor.InsertLoop(50);
        var loop = Assert.IsType<EditLoop>(editor.Root[0]);

        Assert.Equal(ProgramEditor.MaxLoopCount, loop.Count);
        editor.SetLoopCount(loop, 1);
        Assert.Equal(ProgramEditor.MinLoopCount, loop.Count);
    }

    [Fact]
    public void BackspaceRemovesBlockBeforeCursorWithContents()
    {
        var editor = Editor("F 3(F R) L");
        editor.MoveLeft();

        Assert.True(editor.Backspace());
        Assert.Equal("F L", Text(editor));
        Assert.True(editor.Backspace());
        Assert.Equal("L", Text(editor));
        Assert.False(editor.Backspace());
    }

    [Fact]
    public void BackspaceAtStartOfEmptyBlockRemovesIt()
    {
        var editor = new ProgramEditor();
        editor.InsertCommand(Command.Forward);
        editor.InsertIf();

        Assert.True(editor.Backspace());
        Assert.Equal("F", Text(editor));
        Assert.Equal(1, editor.Cursor.Index);
    }

    [Fact]
    public void BackspaceAtStartOfFilledBranchDoesNothing()
    {
        var editor = new ProgramEditor();
        editor.InsertIf();
        editor.InsertCommand(Command.Forward);
        editor.MoveLeft();

        Assert.False(editor.Backspace());
        Assert.Equal("?(F|)", Text(editor));
    }

    [Fact]
    public void RemoveKeepsCursorAtSameBlock()
    {
        var editor = Editor("F L R");

        Assert.True(editor.Remove(editor.Root[0]));

        Assert.Equal("L R", Text(editor));
        Assert.Equal(2, editor.Cursor.Index);
    }

    [Fact]
    public void RemoveNestedCommand()
    {
        var editor = Editor("F 2(L ?(B|R))");
        var loop = Assert.IsType<EditLoop>(editor.Root[1]);
        var iff = Assert.IsType<EditIf>(loop.Body[1]);

        Assert.True(editor.Remove(iff.Else[0]));

        Assert.Equal("F 2(L ?(B|))", Text(editor));
    }

    [Fact]
    public void RemoveBlockWithCursorInsideMovesCursorToItsPlace()
    {
        var editor = Editor("F 3(F R) L");
        var loop = editor.Root[1];
        editor.SetCursor(new EditorSlot(((EditLoop)loop).Body, 1));

        Assert.True(editor.Remove(loop));

        Assert.Equal("F L", Text(editor));
        Assert.Equal(new EditorSlot((List<EditNode>)editor.Root, 1), editor.Cursor);
    }

    [Fact]
    public void RemoveUnknownNodeDoesNothing()
    {
        var editor = Editor("F");

        Assert.False(editor.Remove(new EditCommand(Command.Back)));
        Assert.Equal("F", Text(editor));
    }

    [Fact]
    public void CursorVisitsEverySlotInReadingOrder()
    {
        var editor = Editor("?(F|L) 2(B)");
        var start = editor.AllSlots()[0];
        editor.SetCursor(start);

        var visited = 1;
        while (editor.MoveRight())
        {
            visited++;
        }

        // корень: 0, Да: 0,1, Нет: 0,1, после условия, тело цикла: 0,1, после цикла
        Assert.Equal(9, visited);
        Assert.Equal(editor.AllSlots().Count, visited);
        Assert.False(editor.MoveRight());
    }

    [Fact]
    public void ClearEmptiesProgram()
    {
        var editor = Editor("F F 2(L)");

        editor.Clear();

        Assert.True(editor.IsEmpty);
        Assert.Equal(0, editor.BlockCount);
    }

    [Fact]
    public void BlockLimitIsEnforced()
    {
        var editor = new ProgramEditor();
        for (var i = 0; i < ProgramEditor.MaxBlocks; i++)
        {
            Assert.True(editor.InsertCommand(Command.Forward));
        }

        Assert.False(editor.InsertCommand(Command.Forward));
        Assert.False(editor.InsertLoop());
    }

    [Fact]
    public void ChangedIsRaised()
    {
        var editor = new ProgramEditor();
        var count = 0;
        editor.Changed += () => count++;

        editor.InsertCommand(Command.Forward);
        editor.Backspace();

        Assert.True(count >= 2);
    }
}
