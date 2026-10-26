using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Writer.Animation;
using Writer.Ecs;
using Writer.Systems.Input;
using Writer.Systems.Layout;
using Writer.Tests.Support;
using Writer.Tools.Animator;
using Writer.Tools.Animator.Files;

namespace Writer.Tests.Animation.Tool;

/// <summary>
/// Builds the real animator layout and session on a headless store. Tests add
/// the systems they exercise and drive pointer state and <see cref="Pressed"/>
/// tags by hand, as the generic UI input system would.
/// </summary>
public abstract class AnimatorToolTest : EcsSystemTest
{
    protected static readonly Vector2 Screen = new(1600, 900);

    protected FakePointer Pointer { get; private set; } = null!;
    protected FakeKeyboard Keyboard { get; private set; } = null!;
    protected FakeAnimatorFiles Files { get; private set; } = null!;
    protected Entity Session { get; private set; }

    protected AnimatorUi Ui => Session.GetComponent<AnimatorUi>();
    protected XafPackage Package => Session.GetComponent<AnimatorDocument>().Package;
    protected AnimatorRuntime Runtime => Session.GetComponent<AnimatorDocument>().Runtime;

    protected override void OnEcsSetUp()
    {
        Pointer = new FakePointer();
        Keyboard = new FakeKeyboard();
        Files = new FakeAnimatorFiles();
        Root.Add(new ContainerLayoutSystem());
        AddSystems();
        Session = AnimatorSession.Create(Store, CreatePackage(), Files, "res://test.xaf");
        Session.AddComponent(AnimatorLayout.Build(
            Store,
            Screen,
            new FakeVisualRidFactory()));
        Frame(3);
    }

    /// <summary>Adds the systems under test; runs once the fakes exist.</summary>
    protected virtual void AddSystems() { }

    /// <summary>Root, torso (0,-80), head (0,-70); body attachment on torso; clip "idle".</summary>
    protected virtual XafPackage CreatePackage()
    {
        var package = new XafPackage();
        Bone(package, "root", -1, Vector2.Zero);
        Bone(package, "torso", 0, new Vector2(0, -80));
        Bone(package, "head", 1, new Vector2(0, -70));
        package.Attachments.Add(new XafAttachment
        {
            SvgPath = "res://body.svg",
            Group = "body",
            BoneId = "torso",
            Transform = new Transform2D(0f, new Vector2(0, -20)),
        });

        var clip = new XafClip { Id = "idle", Duration = 2f };
        var channel = new XafChannel
        {
            Target = XafChannelTarget.Bone,
            TargetId = "head",
            Property = XafChannelProperty.Rotation,
        };
        foreach (float time in new[] { 0f, 1f, 2f })
            channel.Keyframes.Add(new XafKeyframe { Time = time, Value = time });
        clip.Channels.Add(channel);
        package.Clips.Add(clip);
        package.Clips.Add(new XafClip { Id = "jump", Duration = 1f });
        return package;
    }

    protected XafClip Idle => Package.Clips[0];

    protected Entity Button(ToolActionKind kind, string argument = "")
    {
        foreach (Entity entity in Store.Query<ToolAction>().ToEntityList())
        {
            ToolAction action = entity.GetComponent<ToolAction>();
            if (action.Kind == kind && action.Argument == argument)
                return entity;
        }

        Assert.Fail($"No button for {kind} '{argument}'.");
        return default;
    }

    /// <summary>The tree label for a bone, as the user would click it.</summary>
    protected Entity BoneLabel(string boneId) =>
        UiTreeControl.FindNode(Ui.BoneList, boneId).GetComponent<UiTreeNode>().Label;

    protected Entity BoneArrow(string boneId) =>
        UiTreeControl.FindNode(Ui.BoneList, boneId).GetComponent<UiTreeNode>().Disclosure;

    /// <summary>Bone ids shown in the tree, top to bottom, skipping hidden rows.</summary>
    protected List<string> VisibleBones()
    {
        var ids = new List<string>();
        foreach (Entity row in Ui.BoneList.ChildEntities)
        {
            if (!row.Tags.Has<LayoutIgnore>())
                ids.Add(row.GetComponent<UiTreeNode>().Id);
        }
        return ids;
    }

    protected Entity ModeButton(AnimatorMode mode)
    {
        foreach (Entity entity in Store.Query<ModeToggle>().ToEntityList())
        {
            if (entity.GetComponent<ModeToggle>().Mode == mode)
                return entity;
        }

        Assert.Fail($"No toggle for {mode}.");
        return default;
    }

    /// <summary>Presses the entity for one frame, then releases it for another.</summary>
    protected void Click(Entity entity)
    {
        entity.AddTag<Pressed>();
        Frame();
        entity.RemoveTag<Pressed>();
        Frame();
    }

    protected Vector2 PreviewOrigin() =>
        AnimatorGeometry.PreviewOrigin(AnimatorGeometry.PanelArea(Ui.Preview));

    protected AnimatorTimelineModel Model() => AnimatorTimelineModel.Create(Session)!;

    /// <summary>Screen position of one key set on the timeline.</summary>
    protected Vector2 KeyPoint(string boneId, float seconds)
    {
        AnimatorTimelineModel model = Model();
        int row = -1;
        for (int i = 0; i < model.Tracks.Count; i++)
        {
            if (model.Tracks[i].Id == boneId)
                row = i - model.FirstRow;
        }

        Assert.That(row, Is.GreaterThanOrEqualTo(0), $"No visible row for {boneId}.");
        return new Vector2(
            model.XForSeconds(seconds),
            model.RowRect(row).Position.Y + AnimatorTimelineModel.RowHeight / 2);
    }

    /// <summary>Screen position of a bone in the current preview pose.</summary>
    protected Vector2 BoneScreenPosition(int bone)
    {
        AnimatorDocument document = Session.GetComponent<AnimatorDocument>();
        XafPose pose = AnimatorGeometry.PreviewPose(
            document,
            Session.GetComponent<AnimatorModes>());
        return PreviewOrigin() + XafRig.WorldBones(document.Package, pose)[bone].Origin;
    }

    private static void Bone(XafPackage package, string id, int parent, Vector2 offset) =>
        package.Skeleton.Bones.Add(new XafBone
        {
            Id = id,
            Parent = parent,
            Rest = new Transform2D(0f, offset),
        });

    protected sealed class FakePointer : IPointerInputSource, IAnimatorWheel
    {
        public Vector2 Position;
        public bool Pressed;
        public bool Secondary;
        public bool Shift;
        public int Wheel;
        public bool Control { get; set; }

        public PointerInput Read() => new(Position, Pressed, Shift, Secondary);

        public int TakeWheel()
        {
            int wheel = Wheel;
            Wheel = 0;
            return wheel;
        }
    }

    protected sealed class FakeAnimatorFiles : IAnimatorFiles
    {
        public XafPackage? ToLoad;
        public XafPackage? Saved;
        public bool FailSave;
        public string? LoadedPath;
        public string? SavedPath;

        public XafPackage? Load(string path)
        {
            LoadedPath = path;
            return ToLoad;
        }

        public void Save(string path, XafPackage package)
        {
            if (FailSave)
                throw new XafFormatException("invalid");
            SavedPath = path;
            Saved = package;
        }
    }
}
