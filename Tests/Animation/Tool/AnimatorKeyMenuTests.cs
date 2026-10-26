using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Commands;
using Writer.Tools.Animator.Systems;
using Position = Writer.Ecs.Position;

namespace Writer.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorKeyMenuTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorKeyMenuSystem(Pointer));
    }

    private AnimatorKeyMenu Menu => Session.GetComponent<AnimatorKeyMenu>();

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    protected override XafPackage CreatePackage()
    {
        XafPackage package = base.CreatePackage();
        // The head also has an eased scale channel, and the body attachment moves.
        XafChannel scale = new()
        {
            Target = XafChannelTarget.Bone,
            TargetId = "head",
            Property = XafChannelProperty.ScaleX,
        };
        scale.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 1f, Interpolation = XafInterpolation.CubicBezier });
        scale.Keyframes.Add(new XafKeyframe { Time = 2f, Value = 3f, Interpolation = XafInterpolation.Linear });
        package.Clips[0].Channels.Add(scale);
        XafChannel body = new()
        {
            Target = XafChannelTarget.Attachment,
            TargetId = "body",
            Property = XafChannelProperty.TranslationX,
        };
        body.Keyframes.Add(new XafKeyframe { Time = 0f, Value = 0f });
        body.Keyframes.Add(new XafKeyframe { Time = 2f, Value = 10f });
        package.Clips[0].Channels.Add(body);
        return package;
    }

    private XafChannel? Channel(string target, XafChannelProperty property) =>
        Idle.Channels.Find(channel => channel.TargetId == target && channel.Property == property);

    private bool HasKeyAt(string target, XafChannelProperty property, float time) =>
        Channel(target, property)?.Keyframes.Exists(key => System.Math.Abs(key.Time - time) < 0.0001f) ?? false;

    /// <summary>A point on a track's row at a time, in the key area.</summary>
    private Vector2 RowPoint(string track, float seconds)
    {
        Vector2 point = KeyPoint(track, 0f);
        return new Vector2(Model().XForSeconds(seconds), point.Y);
    }

    private void RightClick(Vector2 position, bool shift = false)
    {
        Pointer.Position = position;
        Pointer.Shift = shift;
        Pointer.Secondary = true;
        Frame();
        Pointer.Secondary = false;
        Frame();
    }

    private void ChooseOption(Entity option)
    {
        Pointer.Pressed = true;
        option.AddTag<Pressed>();
        Frame();
        Pointer.Pressed = false;
        option.RemoveTag<Pressed>();
        Frame();
    }

    [Test]
    public void RightClickingARowOpensTheMenuAtThePointer()
    {
        Vector2 point = RowPoint("head", 0.5f);

        RightClick(point);

        Assert.That(Menu.Open, Is.True);
        Assert.That(Menu.Track, Is.EqualTo(new XafTrack(XafChannelTarget.Bone, "head")));
        Assert.That(Ui.KeyMenu.GetComponent<Position>().Value,
            Is.EqualTo(point + AnimatorLayout.KeyMenuSize / 2));
        Assert.That(Selection.Bone, Is.EqualTo(2));
    }

    [Test]
    public void ClickingEmptySpaceWithKeyingRemembersTheSnappedTime()
    {
        // Off the keys, so no key is under the pointer.
        RightClick(RowPoint("head", 0.52f));

        Assert.That(Selection.HasKey, Is.False);
        double step = Model().SnapStep;
        Assert.That(Menu.Time, Is.EqualTo((float)(System.Math.Round(0.52 / step) * step)).Within(0.0001f));
    }

    [Test]
    public void ShiftKeepsTheExactTime()
    {
        RightClick(RowPoint("head", 0.52f), shift: true);

        Assert.That(Menu.Time, Is.EqualTo(0.52f).Within(0.01f));
    }

    [Test]
    public void AddKeyKeysEveryChannelOfTheTrackAtTheSampledValue()
    {
        RightClick(RowPoint("head", 0.5f));
        float time = Menu.Time;

        ChooseOption(Ui.KeyMenuAdd);

        // Rotation runs 0 -> 1 over the first second: halfway is 0.5.
        XafChannel rotation = Channel("head", XafChannelProperty.Rotation)!;
        Assert.That(rotation.Keyframes.Find(key => System.Math.Abs(key.Time - time) < 0.0001f)!.Value,
            Is.EqualTo(XafSampler.SampleChannel(
                new XafChannel { Keyframes = { new XafKeyframe { Time = 0, Value = 0 }, new XafKeyframe { Time = 1, Value = 1 } } },
                time)).Within(0.001f));
        Assert.That(HasKeyAt("head", XafChannelProperty.ScaleX, time), Is.True);
        // Only the channels the track already animates are keyed.
        Assert.That(Channel("head", XafChannelProperty.TranslationX), Is.Null);
        Assert.That(Menu.Open, Is.False);
        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(time).Within(0.0001f));
    }

    [Test]
    public void AddingAKeyDoesNotChangeTheAnimation()
    {
        float[] probes = { 0.1f, 0.5f, 0.9f, 1.4f, 1.9f };
        var before = new float[probes.Length];
        for (int i = 0; i < probes.Length; i++)
            before[i] = XafSampler.Sample(Package, Idle, probes[i]).Bones[2].Rotation;

        RightClick(RowPoint("head", 0.5f));
        ChooseOption(Ui.KeyMenuAdd);

        for (int i = 0; i < probes.Length; i++)
        {
            Assert.That(XafSampler.Sample(Package, Idle, probes[i]).Bones[2].Rotation,
                Is.EqualTo(before[i]).Within(0.001f), $"at {probes[i]}s");
        }
    }

    [Test]
    public void ANewKeyKeepsTheEasingOfTheSegmentItSplits()
    {
        RightClick(RowPoint("head", 1f));
        // Keys exist at 0 (eased) and 2: a key at ~1 splits that eased segment.
        RightClick(RowPoint("head", 0.9f));
        float time = Menu.Time;

        ChooseOption(Ui.KeyMenuAdd);

        XafKeyframe added = Channel("head", XafChannelProperty.ScaleX)!.Keyframes
            .Find(key => System.Math.Abs(key.Time - time) < 0.0001f)!;
        Assert.That(added.Interpolation, Is.EqualTo(XafInterpolation.CubicBezier));
    }

    [Test]
    public void AddKeyIsOneUndoStep()
    {
        RightClick(RowPoint("head", 0.5f));
        float time = Menu.Time;
        ChooseOption(Ui.KeyMenuAdd);
        Assert.That(HasKeyAt("head", XafChannelProperty.Rotation, time), Is.True);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(HasKeyAt("head", XafChannelProperty.Rotation, time), Is.False);
        Assert.That(HasKeyAt("head", XafChannelProperty.ScaleX, time), Is.False);
    }

    [Test]
    public void AddKeyOnAKeyThatExistsAddsNothing()
    {
        // Exactly on the key at one second.
        RightClick(RowPoint("head", 1f));
        int keys = Channel("head", XafChannelProperty.Rotation)!.Keyframes.Count;

        ChooseOption(Ui.KeyMenuAdd);

        Assert.That(Channel("head", XafChannelProperty.Rotation)!.Keyframes, Has.Count.EqualTo(keys));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("KEY ALREADY HERE"));
    }

    [Test]
    public void RightClickingAKeySelectsItAndDeleteKeyRemovesIt()
    {
        RightClick(RowPoint("head", 1f));

        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(1f));

        ChooseOption(Ui.KeyMenuDelete);

        Assert.That(HasKeyAt("head", XafChannelProperty.Rotation, 1f), Is.False);
        Assert.That(HasKeyAt("head", XafChannelProperty.ScaleX, 1f), Is.False);
        // Other times and targets are untouched.
        Assert.That(HasKeyAt("head", XafChannelProperty.Rotation, 0f), Is.True);
        Assert.That(HasKeyAt("body", XafChannelProperty.TranslationX, 0f), Is.True);
        Assert.That(Selection.HasKey, Is.False);
    }

    [Test]
    public void DeleteKeyIsOneUndoStep()
    {
        RightClick(RowPoint("head", 1f));
        ChooseOption(Ui.KeyMenuDelete);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(HasKeyAt("head", XafChannelProperty.Rotation, 1f), Is.True);
    }

    [Test]
    public void DeleteKeyWithNoKeyUnderThePointerSaysSoAndDeletesNothing()
    {
        RightClick(RowPoint("head", 0.52f));
        int keys = Channel("head", XafChannelProperty.Rotation)!.Keyframes.Count;

        ChooseOption(Ui.KeyMenuDelete);

        Assert.That(Channel("head", XafChannelProperty.Rotation)!.Keyframes, Has.Count.EqualTo(keys));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("NO KEY SELECTED"));
    }

    [Test]
    public void AddKeyWorksOnAnAttachmentTrack()
    {
        RightClick(RowPoint("body", 1f));
        // The attachment has keys at 0 and 2 only, so one second is empty.
        Assert.That(Menu.Track, Is.EqualTo(new XafTrack(XafChannelTarget.Attachment, "body")));

        ChooseOption(Ui.KeyMenuAdd);

        XafKeyframe added = Channel("body", XafChannelProperty.TranslationX)!.Keyframes
            .Find(key => System.Math.Abs(key.Time - Menu.Time) < 0.0001f)!;
        Assert.That(added.Value, Is.EqualTo(5f).Within(0.5f));
        Assert.That(Selection.Attachment, Is.EqualTo(0));
    }

    [Test]
    public void ClicksOutsideTheKeyAreaDoNotOpenTheMenu()
    {
        // The label column, the ruler, and empty rows below the tracks.
        RightClick(new Vector2(Model().Labels.Position.X + 20, Model().RowRect(0).GetCenter().Y));
        Assert.That(Menu.Open, Is.False);

        RightClick(Model().Ruler.GetCenter());
        Assert.That(Menu.Open, Is.False);

        RightClick(PreviewOrigin());
        Assert.That(Menu.Open, Is.False);
    }

    [Test]
    public void AnyOtherClickClosesTheMenu()
    {
        RightClick(RowPoint("head", 0.5f));
        Assert.That(Menu.Open, Is.True);

        Pointer.Position = PreviewOrigin();
        Pointer.Pressed = true;
        Frame();
        Pointer.Pressed = false;
        Frame();

        Assert.That(Menu.Open, Is.False);
        Assert.That(Ui.KeyMenu.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void RightClickingElsewhereClosesAnOpenMenu()
    {
        RightClick(RowPoint("head", 0.5f));

        RightClick(PreviewOrigin());

        Assert.That(Menu.Open, Is.False);
    }

    [Test]
    public void TheOptionThatDoesNotApplyIsDimmed()
    {
        Color normal = Ui.KeyMenuDelete.GetComponent<ButtonVisual>().Normal;

        RightClick(RowPoint("head", 0.52f));
        Assert.That(Ui.KeyMenuDelete.GetComponent<ButtonVisual>().Normal, Is.Not.EqualTo(normal));
        Assert.That(Ui.KeyMenuAdd.GetComponent<ButtonVisual>().Normal, Is.EqualTo(normal));

        RightClick(RowPoint("head", 1f));
        Assert.That(Ui.KeyMenuDelete.GetComponent<ButtonVisual>().Normal, Is.EqualTo(normal));
        Assert.That(Ui.KeyMenuAdd.GetComponent<ButtonVisual>().Normal, Is.Not.EqualTo(normal));
    }

    [Test]
    public void TheMenuOpensAboveOrLeftOfThePointerNearTheScreenEdges()
    {
        var size = AnimatorLayout.KeyMenuSize;
        var screen = new Vector2(1600, 900);

        Assert.That(AnimatorMenuPlacement.Anchor(new Vector2(100, 100), size, screen),
            Is.EqualTo(new Vector2(100, 100)));
        Assert.That(AnimatorMenuPlacement.Anchor(new Vector2(100, 880), size, screen),
            Is.EqualTo(new Vector2(100, 880 - size.Y)));
        Assert.That(AnimatorMenuPlacement.Anchor(new Vector2(1590, 100), size, screen),
            Is.EqualTo(new Vector2(1590 - size.X, 100)));
        Vector2 corner = AnimatorMenuPlacement.Anchor(new Vector2(1600, 900), size, screen);
        Assert.That(corner.X + size.X, Is.LessThanOrEqualTo(screen.X));
        Assert.That(corner.Y + size.Y, Is.LessThanOrEqualTo(screen.Y));
    }
}
