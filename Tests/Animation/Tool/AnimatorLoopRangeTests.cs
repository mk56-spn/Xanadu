using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;

namespace Writer.Tests.Animation.Tool;

/// <summary>Dragging the loop range handles in the timeline ruler.</summary>
[TestFixture]
public sealed class AnimatorLoopRangeTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorTimelineInputSystem(Pointer, Pointer));
        Root.Add(new AnimatorShortcutSystem(Keyboard));
    }

    private void LoopOn(float start, float end)
    {
        AnimatorClipCommands.ToggleLoop(Session);
        Idle.LoopStart = start;
        Idle.LoopEnd = end;
        Frame();
    }

    private Vector2 RulerPoint(float seconds) =>
        new(Model().XForSeconds(seconds), Model().Ruler.Position.Y + 10f);

    private void Press(Vector2 position)
    {
        Pointer.Position = position;
        Pointer.Pressed = true;
        Ui.Timeline.AddTag<Pressed>();
        Frame();
    }

    private void MoveTo(Vector2 position)
    {
        Pointer.Position = position;
        Frame();
    }

    private void Release()
    {
        Pointer.Pressed = false;
        Ui.Timeline.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void TheRulerOnlyOffersLoopHandlesWhenLooping()
    {
        Assert.That(Model().TryPickLoopHandle(RulerPoint(0f), out _), Is.False);

        LoopOn(0.5f, 1.5f);

        Assert.That(Model().TryPickLoopHandle(RulerPoint(0.5f), out bool start), Is.True);
        Assert.That(start, Is.True);
        Assert.That(Model().TryPickLoopHandle(RulerPoint(1.5f), out start), Is.True);
        Assert.That(start, Is.False);
    }

    [Test]
    public void DraggingTheStartHandleMovesTheLoopStart()
    {
        LoopOn(0f, 2f);

        Press(RulerPoint(0f));
        MoveTo(RulerPoint(0.5f));
        Release();

        Assert.That(Idle.LoopStart, Is.EqualTo(0.5f).Within(0.03f));
        Assert.That(Idle.LoopEnd, Is.EqualTo(2f).Within(0.001f));
    }

    [Test]
    public void TheLoopHandleDragIsOneUndoStep()
    {
        LoopOn(0f, 2f);

        Press(RulerPoint(0f));
        MoveTo(RulerPoint(0.5f));
        Release();
        float moved = Idle.LoopStart;

        AnimatorHistoryCommands.Undo(Session);

        Assert.That(moved, Is.GreaterThan(0.1f));
        Assert.That(Idle.LoopStart, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void DraggingPastTheOtherEndKeepsTheMinimumLength()
    {
        LoopOn(0.25f, 1.75f);

        Press(RulerPoint(1.75f));
        MoveTo(RulerPoint(0.1f));
        Release();

        Assert.That(Idle.LoopEnd, Is.GreaterThanOrEqualTo(Idle.LoopStart + XafClipAuthoring.MinDuration - 0.001f));
        Assert.That(Idle.LoopEnd, Is.LessThanOrEqualTo(Idle.Duration));
    }

    [Test]
    public void TheAuthoringHelpersClampBothEnds()
    {
        var clip = new XafClip
        {
            Id = "idle",
            Duration = 2f,
            Loop = true,
            LoopStart = 0.5f,
            LoopEnd = 1.5f,
        };

        Assert.That(XafClipAuthoring.SetLoopStart(clip, 9f), Is.EqualTo(1.4f).Within(0.0001f));
        Assert.That(XafClipAuthoring.SetLoopEnd(clip, 0f), Is.EqualTo(1.5f).Within(0.0001f));
        Assert.That(XafClipAuthoring.SetLoopEnd(clip, 9f), Is.EqualTo(2f).Within(0.0001f));
    }
}
