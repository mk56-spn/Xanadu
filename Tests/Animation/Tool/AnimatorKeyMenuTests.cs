using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Model.Animation;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Tests.Animation.Tool;

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

    protected override RigPackage CreatePackage()
    {
        RigPackage package = base.CreatePackage();
        // The head also has an eased scale channel, and the body attachment moves.
        RigChannel scale = new()
        {
            Target = RigChannelTarget.Bone,
            TargetId = "head",
            Property = RigChannelProperty.ScaleX
        };
        scale.Keyframes.Add(new RigKeyframe { Time = 0f, Value = 1f, Interpolation = RigInterpolation.CubicBezier });
        scale.Keyframes.Add(new RigKeyframe { Time = 2f, Value = 3f, Interpolation = RigInterpolation.Linear });
        package.Clips[0].Channels.Add(scale);
        RigChannel body = new()
        {
            Target = RigChannelTarget.Attachment,
            TargetId = "body",
            Property = RigChannelProperty.TranslationX
        };
        body.Keyframes.Add(new RigKeyframe { Time = 0f, Value = 0f });
        body.Keyframes.Add(new RigKeyframe { Time = 2f, Value = 10f });
        package.Clips[0].Channels.Add(body);
        return package;
    }

    private RigChannel? Channel(string target, RigChannelProperty property) =>
        Idle.Channels.Find(channel => channel.TargetId == target && channel.Property == property);

    private bool HasKeyAt(string target, RigChannelProperty property, float time) =>
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
        Assert.That(Menu.Track, Is.EqualTo(new RigTrack(RigChannelTarget.Bone, "head")));
        Assert.That(Ui.KeyMenu.GetComponent<Position>().Value, Is.EqualTo(point + AnimatorLayout.KeyMenuSize / 2));
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
        RightClick(RowPoint("head", 0.52f), true);

        Assert.That(Menu.Time, Is.EqualTo(0.52f).Within(0.01f));
    }

    [Test]
    public void AddKeyKeysEveryChannelOfTheTrackAtTheSampledValue()
    {
        RightClick(RowPoint("head", 0.5f));
        float time = Menu.Time;

        ChooseOption(Ui.KeyMenuAdd);

        // Rotation runs 0 -> 1 over the first second: halfway is 0.5.
        RigChannel rotation = Channel("head", RigChannelProperty.Rotation)!;
        Assert.That(
            rotation.Keyframes.Find(key => System.Math.Abs(key.Time - time) < 0.0001f)!.Value,
            Is.EqualTo(
                RigSampler.SampleChannel(
                    new RigChannel
                    {
                        Keyframes = { new RigKeyframe { Time = 0, Value = 0 }, new RigKeyframe { Time = 1, Value = 1 } }
                    },
                    time)).Within(0.001f));
        Assert.That(HasKeyAt("head", RigChannelProperty.ScaleX, time), Is.True);
        // Only the channels the track already animates are keyed.
        Assert.That(Channel("head", RigChannelProperty.TranslationX), Is.Null);
        Assert.That(Menu.Open, Is.False);
        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(time).Within(0.0001f));
    }

    [Test]
    public void AddingAKeyDoesNotChangeTheAnimation()
    {
        float[] probes = { 0.1f, 0.5f, 0.9f, 1.4f, 1.9f };
        float[] before = new float[probes.Length];
        for (int i = 0; i < probes.Length; i++)
            before[i] = RigSampler.Sample(Package, Idle, probes[i]).Bones[2].Rotation;

        RightClick(RowPoint("head", 0.5f));
        ChooseOption(Ui.KeyMenuAdd);

        for (int i = 0; i < probes.Length; i++)
        {
            Assert.That(
                RigSampler.Sample(Package, Idle, probes[i]).Bones[2].Rotation,
                Is.EqualTo(before[i]).Within(0.001f),
                $"at {probes[i]}s");
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

        RigKeyframe added =
            Channel("head", RigChannelProperty.ScaleX)!.Keyframes.Find(key =>
                System.Math.Abs(key.Time - time) < 0.0001f)!;
        Assert.That(added.Interpolation, Is.EqualTo(RigInterpolation.CubicBezier));
    }

    [Test]
    public void AddKeyIsOneUndoStep()
    {
        RightClick(RowPoint("head", 0.5f));
        float time = Menu.Time;
        ChooseOption(Ui.KeyMenuAdd);
        Assert.That(HasKeyAt("head", RigChannelProperty.Rotation, time), Is.True);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(HasKeyAt("head", RigChannelProperty.Rotation, time), Is.False);
        Assert.That(HasKeyAt("head", RigChannelProperty.ScaleX, time), Is.False);
    }

    [Test]
    public void AddKeyOnAKeyThatExistsAddsNothing()
    {
        // Exactly on the key at one second.
        RightClick(RowPoint("head", 1f));
        int keys = Channel("head", RigChannelProperty.Rotation)!.Keyframes.Count;

        ChooseOption(Ui.KeyMenuAdd);

        Assert.That(Channel("head", RigChannelProperty.Rotation)!.Keyframes, Has.Count.EqualTo(keys));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("KEY ALREADY HERE"));
    }

    [Test]
    public void RightClickingAKeySelectsItAndDeleteKeyRemovesIt()
    {
        RightClick(RowPoint("head", 1f));

        Assert.That(Selection.HasKey, Is.True);
        Assert.That(Selection.KeyTime, Is.EqualTo(1f));

        ChooseOption(Ui.KeyMenuDelete);

        Assert.That(HasKeyAt("head", RigChannelProperty.Rotation, 1f), Is.False);
        Assert.That(HasKeyAt("head", RigChannelProperty.ScaleX, 1f), Is.False);
        // Other times and targets are untouched.
        Assert.That(HasKeyAt("head", RigChannelProperty.Rotation, 0f), Is.True);
        Assert.That(HasKeyAt("body", RigChannelProperty.TranslationX, 0f), Is.True);
        Assert.That(Selection.HasKey, Is.False);
    }

    [Test]
    public void DeleteKeyIsOneUndoStep()
    {
        RightClick(RowPoint("head", 1f));
        ChooseOption(Ui.KeyMenuDelete);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.Undo));

        Assert.That(HasKeyAt("head", RigChannelProperty.Rotation, 1f), Is.True);
    }

    [Test]
    public void DeleteKeyWithNoKeyUnderThePointerSaysSoAndDeletesNothing()
    {
        RightClick(RowPoint("head", 0.52f));
        int keys = Channel("head", RigChannelProperty.Rotation)!.Keyframes.Count;

        ChooseOption(Ui.KeyMenuDelete);

        Assert.That(Channel("head", RigChannelProperty.Rotation)!.Keyframes, Has.Count.EqualTo(keys));
        Assert.That(Session.GetComponent<AnimatorStatus>().Message, Is.EqualTo("NO KEY SELECTED"));
    }

    [Test]
    public void AddKeyWorksOnAnAttachmentTrack()
    {
        RightClick(RowPoint("body", 1f));
        // The attachment has keys at 0 and 2 only, so one second is empty.
        Assert.That(Menu.Track, Is.EqualTo(new RigTrack(RigChannelTarget.Attachment, "body")));

        ChooseOption(Ui.KeyMenuAdd);

        RigKeyframe added =
            Channel("body", RigChannelProperty.TranslationX)!.Keyframes.Find(key =>
                System.Math.Abs(key.Time - Menu.Time) < 0.0001f)!;
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
        Assert.That(UiLayout.IsHidden(Ui.KeyMenu), Is.True);
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
        Vector2 size = AnimatorLayout.KeyMenuSize;
        var screen = new Vector2(1600, 900);

        Assert.That(
            AnimatorMenuPlacement.Anchor(new Vector2(100, 100), size, screen),
            Is.EqualTo(new Vector2(100, 100)));
        Assert.That(
            AnimatorMenuPlacement.Anchor(new Vector2(100, 880), size, screen),
            Is.EqualTo(new Vector2(100, 880 - size.Y)));
        Assert.That(
            AnimatorMenuPlacement.Anchor(new Vector2(1590, 100), size, screen),
            Is.EqualTo(new Vector2(1590 - size.X, 100)));
        Vector2 corner = AnimatorMenuPlacement.Anchor(new Vector2(1600, 900), size, screen);
        Assert.That(corner.X + size.X, Is.LessThanOrEqualTo(screen.X));
        Assert.That(corner.Y + size.Y, Is.LessThanOrEqualTo(screen.Y));
    }
}
