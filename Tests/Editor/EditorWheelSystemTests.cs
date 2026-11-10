using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorWheelSystemTests
{
    private EntityStore _store = null!;
    private SystemRoot _root = null!;
    private EditorCommandBus _commands = null!;
    private PointerLatch _wheel = null!;
    private Entity _editor;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _commands = new EditorCommandBus();
        _wheel = new PointerLatch();
        _editor = _store.CreateEntity(
            new Playhead(1_000, 120, 0, false, durationMs: 5_000),
            new PlayfieldView(),
            new EditorViewNavigation(new Vector2(100, 100)));
        _root = new SystemRoot(_store);
        _root.Add(new EditorWheelSystem(_commands, _wheel));
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private long TakeSeek()
    {
        Assert.That(_commands.TryDequeue(out EditorCommand command), Is.True);
        Assert.That(command.Type, Is.EqualTo(EditorCommandType.Seek));
        return command.LongValue;
    }

    [Test]
    public void WheelUpSeeksBackAndDownSeeksForward()
    {
        _wheel.AddWheel(1, control: false, shift: false);
        Frame();
        Assert.That(TakeSeek(), Is.EqualTo(500));

        _wheel.AddWheel(-1, control: false, shift: false);
        Frame();
        Assert.That(TakeSeek(), Is.EqualTo(1_500));
        Assert.That(_wheel.Notches, Is.Zero);
    }

    [Test]
    public void ShiftSeeksFurther()
    {
        _wheel.AddWheel(-1, control: false, shift: true);
        Frame();

        Assert.That(TakeSeek(), Is.EqualTo(3_000));
    }

    [Test]
    public void NotchesInOneFrameAllCount()
    {
        _wheel.AddWheel(-1, control: false, shift: false);
        _wheel.AddWheel(-1, control: false, shift: false);
        Frame();

        Assert.That(TakeSeek(), Is.EqualTo(2_000));
    }

    [Test]
    public void ControlZoomsInsteadOfSeeking()
    {
        _wheel.AddWheel(1, control: true, shift: false);
        Frame();

        Assert.That(_editor.GetComponent<EditorViewNavigation>().ZoomSteps, Is.EqualTo(1));
        Assert.That(_commands.Count, Is.Zero);
    }

    [Test]
    public void AFocusedTextFieldKeepsTheWheelFromTheEditor()
    {
        Entity field = _store.CreateEntity(new TextField());
        field.AddTag<TextFieldFocused>();

        _wheel.AddWheel(1, control: false, shift: false);
        Frame();

        Assert.That(_commands.Count, Is.Zero);
    }
}
