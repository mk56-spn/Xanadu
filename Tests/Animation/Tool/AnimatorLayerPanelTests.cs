using System.Linq;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Animation.Tool;

[TestFixture]
public sealed class AnimatorLayerPanelTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTabSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
        Root.Add(new AnimatorLayerPanelSystem(new FakeVisualRidFactory()));
    }

    private void Art(params (string Group, int Layer)[] art)
    {
        Package.Attachments.Clear();
        foreach ((string group, int layer) in art)
        {
            Package.Attachments.Add(new XafAttachment
            {
                SvgPath = $"res://{group}.svg",
                Group = group,
                BoneId = "torso",
                ZOrder = layer,
            });
        }
        AnimatorSession.RigChanged(Session);

        Ui.Tabs.GetComponent<UiTabs>().Selected = AnimatorLayout.LayersTab;
        Frame(3);
    }

    private string[] Shown() => Ui.LayerList.ChildEntities
        .Select(row => row.ChildEntities[0].GetComponent<Text>().Value)
        .ToArray();

    private Entity Row(string group) => Ui.LayerList.ChildEntities
        .First(row => row.ChildEntities[0].GetComponent<Text>().Value.EndsWith(group));

    private int Layer(string group) => Package.Attachments.First(art => art.Group == group).ZOrder;

    [Test]
    public void RowsListTheArtFrontFirstAndShowItsLayer()
    {
        Art(("legs", 0), ("hair", 5), ("head", 3));

        Assert.That(Shown(), Is.EqualTo(new[] { "5   hair", "3   head", "0   legs" }));
    }

    [Test]
    public void OnOneLayerLaterArtIsListedInFront()
    {
        Art(("first", 2), ("second", 2));

        Assert.That(Shown(), Is.EqualTo(new[] { "2   second", "2   first" }));
    }

    [Test]
    public void ClothIsListedWithTheArtAndAboveItOnTheSameLayer()
    {
        Package.Cloths.Add(new XafCloth { Id = "dress", BoneId = "torso", ZOrder = 2 });
        Art(("legs", 2));

        Assert.That(Shown(), Is.EqualTo(new[] { "2   dress", "2   legs" }));
    }

    [Test]
    public void ClickingARowSelectsItsArt()
    {
        Art(("legs", 0), ("hair", 5));

        Click(Row("legs").ChildEntities[0]);

        Assert.That(Session.GetComponent<AnimatorSelection>().Attachment, Is.EqualTo(0));
        Assert.That(
            Row("legs").ChildEntities[0].GetComponent<ButtonVisual>().Normal,
            Is.Not.EqualTo(Row("hair").ChildEntities[0].GetComponent<ButtonVisual>().Normal));
    }

    [Test]
    public void MovingForwardSwapsLayersWithTheRowAbove()
    {
        Art(("legs", 0), ("hair", 5));

        Click(Row("legs").ChildEntities[1]);

        Assert.That(Layer("legs"), Is.EqualTo(5));
        Assert.That(Layer("hair"), Is.EqualTo(0));
        Assert.That(Shown(), Is.EqualTo(new[] { "5   legs", "0   hair" }));
        Assert.That(Session.GetComponent<AnimatorSelection>().Attachment, Is.EqualTo(0));
    }

    [Test]
    public void MovingBackPastARowOnTheSameLayerStepsDownOne()
    {
        Art(("first", 2), ("second", 2));

        Click(Row("second").ChildEntities[2]);

        Assert.That(Layer("second"), Is.EqualTo(1));
        Assert.That(Layer("first"), Is.EqualTo(2));
    }

    [Test]
    public void TheEndsOfTheStackCannotMoveFurther()
    {
        Art(("legs", 0), ("hair", 5));

        Assert.That(Row("hair").ChildEntities[1].Tags.Has<UiDisabled>(), Is.True);
        Assert.That(Row("hair").ChildEntities[2].Tags.Has<UiDisabled>(), Is.False);
        Assert.That(Row("legs").ChildEntities[2].Tags.Has<UiDisabled>(), Is.True);

        AnimatorLayerCommands.Move(Session, "a1", 1);
        Assert.That(Layer("hair"), Is.EqualTo(5));
    }

    [Test]
    public void AShortcutMovesTheSelectedArt()
    {
        Art(("legs", 0), ("hair", 5));
        Session.GetComponent<AnimatorSelection>().SelectOnlyAttachment(0);

        AnimatorCommands.Run(Session, new ToolAction(ToolActionKind.MoveLayerForward));

        Assert.That(Layer("legs"), Is.EqualTo(5));
    }

    [Test]
    public void AMoveIsOneUndoStep()
    {
        Art(("legs", 0), ("hair", 5));

        Click(Row("legs").ChildEntities[1]);
        Click(Button(ToolActionKind.Undo));

        Assert.That(Layer("legs"), Is.EqualTo(0));
        Assert.That(Layer("hair"), Is.EqualTo(5));
    }

    [Test]
    public void AKeyedLayerIsKeyedRatherThanEditingItsBase()
    {
        Art(("legs", 0), ("hair", 5));
        XafAttachmentAuthoring.KeyLayer(Package, Idle, 0, 0f, 1);
        Session.GetComponent<AnimatorDocument>().Runtime.Seek(0f);
        Frame(2);
        Assert.That(Shown()[1], Is.EqualTo("1   legs"));

        Click(Row("legs").ChildEntities[1]);

        Assert.That(Layer("legs"), Is.EqualTo(0), "the base layer is left alone");
        Assert.That(Shown()[0], Is.EqualTo("5   legs"));
    }

    [Test]
    public void TheRowsFollowArtBeingAddedAndRemoved()
    {
        Art(("legs", 0), ("hair", 5));
        Assert.That(Ui.LayerList.ChildCount, Is.EqualTo(2));

        Art(("legs", 0));
        Assert.That(Ui.LayerList.ChildCount, Is.EqualTo(1));
    }

    [Test]
    public void ALargeRigDoesNotBuildRowsUntilTheTabIsOpen()
    {
        Assert.That(Ui.LayerList.Enabled, Is.False);
        Frame(2);
        Assert.That(Ui.LayerList.ChildCount, Is.EqualTo(0));
    }
}
