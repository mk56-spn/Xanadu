using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Commands;
using Xanadu.Tools.Animator.Systems;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>Verlet chains in the tool: adding, selecting, editing, deleting and swinging them.</summary>
[TestFixture]
public sealed class AnimatorChainTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorChainInspectorSystem());
        Root.Add(new AnimatorChainSimulationSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
    }

    private AnimatorSelection Selection => Session.GetComponent<AnimatorSelection>();

    private string Status => Session.GetComponent<AnimatorStatus>().Message;

    private Vector2 InspectorCenter => Ui.ChainInspector.GetComponent<Position>().Value;

    private void SelectBone(int bone)
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(bone);
        Frame();
    }

    private void Type(Entity field, string value)
    {
        field.GetComponent<TextField>().Value = value;
        field.AddTag<TextFieldFocused>();
        Frame();
        field.RemoveTag<TextFieldFocused>();
        Frame();
    }

    private void AddChainOnHead()
    {
        SelectBone(2);
        Click(Button(ToolActionKind.AddChain));
    }

    [Test]
    public void AddingHangsAChainOffTheSelectedBoneAndSelectsIt()
    {
        AddChainOnHead();

        Assert.That(Package.Chains, Has.Count.EqualTo(1));
        Assert.That(Package.Chains[0].BoneId, Is.EqualTo("head"));
        Assert.That(Selection.Chain, Is.EqualTo(0));
        Assert.That(Selection.Bone, Is.EqualTo(-1));
        Assert.That(Status, Is.EqualTo("ADDED head_chain"));
        Assert.That(Session.GetComponent<AnimatorFile>().Dirty, Is.True);
    }

    [Test]
    public void AddingWithoutABoneSelectedSaysSo()
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(-1);

        Click(Button(ToolActionKind.AddChain));

        Assert.That(Package.Chains, Is.Empty);
        Assert.That(Status, Is.EqualTo("SELECT A BONE FIRST"));
    }

    [Test]
    public void AddingCanBeUndoneAndRedone()
    {
        AddChainOnHead();

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains, Is.Empty);
        Assert.That(Selection.Chain, Is.EqualTo(-1));

        AnimatorHistoryCommands.Redo(Session);
        Assert.That(Package.Chains, Has.Count.EqualTo(1));
    }

    [Test]
    public void TheChainListShowsEachChainAndSelectingOneSelectsIt()
    {
        AddChainOnHead();
        SelectBone(1);
        Assert.That(Selection.Chain, Is.EqualTo(-1));
        Frame(2);

        Assert.That(Ui.ChainList.ChildEntities.Count, Is.EqualTo(1));
        Click(Button(ToolActionKind.SelectChain, "0"));

        Assert.That(Selection.Chain, Is.EqualTo(0));
        Assert.That(Selection.Bone, Is.EqualTo(-1));
    }

    [Test]
    public void TheInspectorShowsOnlyWhileAChainIsSelected()
    {
        Assert.That(InspectorCenter, Is.EqualTo(UiLayout.Hidden));

        AddChainOnHead();
        Assert.That(
            InspectorCenter,
            Is.EqualTo(AnimatorLayout.ChainInspectorTopLeft(Screen) + AnimatorLayout.ChainInspectorSize / 2));

        SelectBone(1);
        Assert.That(InspectorCenter, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void TheInspectorFieldsShowTheChainsSettings()
    {
        AddChainOnHead();

        Assert.That(Ui.ChainSegments.GetComponent<TextField>().Value, Is.EqualTo("6"));
        Assert.That(Ui.ChainLength.GetComponent<TextField>().Value, Is.EqualTo("20"));
        Assert.That(Ui.ChainGravity.GetComponent<TextField>().Value, Is.EqualTo("900"));
        Assert.That(Ui.ChainDamping.GetComponent<TextField>().Value, Is.EqualTo("0.96"));
        Assert.That(Ui.ChainTitle.GetComponent<Text>().Value, Does.StartWith("head_chain"));
    }

    [Test]
    public void ANewChainIsBuiltFromTheGoldenRhombusByDefault()
    {
        AddChainOnHead();

        XafChain chain = Package.Chains[0];
        Assert.That(chain.SvgPath, Is.EqualTo(XafChain.DefaultSvgPath));
        Assert.That(chain.Group, Is.EqualTo(XafChain.DefaultGroup));
        Assert.That(Ui.ChainWidth.GetComponent<TextField>().Value, Is.EqualTo("10"));
        Assert.That(Ui.ChainLean.GetComponent<TextField>().Value, Is.EqualTo("0"));
        Assert.That(Ui.ChainCollide.GetComponent<TextField>().Value, Is.EqualTo("0"));
    }

    [Test]
    public void WidthLeanAndColliderCanBeTypedAndUndone()
    {
        AddChainOnHead();

        Type(Ui.ChainWidth, "6");
        Type(Ui.ChainLean, "80");
        Type(Ui.ChainCollide, "12");

        XafChain chain = Package.Chains[0];
        Assert.That(chain.Width, Is.EqualTo(6f));
        Assert.That(chain.Lean, Is.EqualTo(80f));
        Assert.That(chain.CollideRadius, Is.EqualTo(12f));
        Assert.That(chain.CollideBoneId, Is.EqualTo("head"), "a first collider goes on the bone the chain hangs from");
        Assert.That(chain.HasCollider, Is.True);

        Type(Ui.ChainCollide, "0");
        Assert.That(Package.Chains[0].HasCollider, Is.False);

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains[0].CollideRadius, Is.EqualTo(12f));
    }

    [Test]
    public void TheChainInspectorEndsAboveTheTimeline()
    {
        Rect2 timeline = AnimatorGeometry.PanelArea(Ui.Timeline);
        float bottom = AnimatorLayout.ChainInspectorTopLeft(Screen).Y + AnimatorLayout.ChainInspectorSize.Y;

        Assert.That(bottom, Is.LessThanOrEqualTo(timeline.Position.Y));
    }

    [Test]
    public void TypingASettingChangesTheChainAsOneUndoStep()
    {
        AddChainOnHead();

        Type(Ui.ChainSegments, "10");
        Type(Ui.ChainLength, "8.5");
        Type(Ui.ChainGravity, "250");
        Type(Ui.ChainDamping, "0.8");

        XafChain chain = Package.Chains[0];
        Assert.That(chain.Segments, Is.EqualTo(10));
        Assert.That(chain.SegmentLength, Is.EqualTo(8.5f));
        Assert.That(chain.Gravity, Is.EqualTo(250f));
        Assert.That(chain.Damping, Is.EqualTo(0.8f));

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains[0].Damping, Is.EqualTo(0.96f));
        Assert.That(Package.Chains[0].Gravity, Is.EqualTo(250f));
    }

    [Test]
    public void ASettingOutOfRangeIsClampedAndShownThatWay()
    {
        AddChainOnHead();

        Type(Ui.ChainSegments, "9999");

        Assert.That(Package.Chains[0].Segments, Is.EqualTo(XafChain.MaxSegments));
        Assert.That(Ui.ChainSegments.GetComponent<TextField>().Value, Is.EqualTo("64"));
    }

    [Test]
    public void TextThatIsNotANumberLeavesTheChainAlone()
    {
        AddChainOnHead();

        Type(Ui.ChainGravity, "-");

        Assert.That(Package.Chains[0].Gravity, Is.EqualTo(900f));
        Assert.That(Status, Is.EqualTo("NOT A NUMBER"));
    }

    [Test]
    public void ATypedValueIsDroppedWhenAnotherChainWasSelectedMeanwhile()
    {
        AddChainOnHead();
        SelectBone(2);
        Click(Button(ToolActionKind.AddChain));
        Assert.That(Selection.Chain, Is.EqualTo(1));

        Ui.ChainGravity.GetComponent<TextField>().Value = "5";
        Ui.ChainGravity.AddTag<TextFieldFocused>();
        Frame();
        Session.GetComponent<AnimatorSelection>().SelectOnlyChain(0);
        Ui.ChainGravity.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(Package.Chains[0].Gravity, Is.EqualTo(900f));
        Assert.That(Package.Chains[1].Gravity, Is.EqualTo(900f));
    }

    [Test]
    public void DeletingRemovesTheChainAndSelectsItsBone()
    {
        AddChainOnHead();

        Click(Button(ToolActionKind.DeleteChain));

        Assert.That(Package.Chains, Is.Empty);
        Assert.That(Selection.Chain, Is.EqualTo(-1));
        Assert.That(Selection.Bone, Is.EqualTo(2));
        Assert.That(Status, Is.EqualTo("DELETED head_chain"));

        AnimatorHistoryCommands.Undo(Session);
        Assert.That(Package.Chains, Has.Count.EqualTo(1));
    }

    [Test]
    public void DeletingWithNoChainSelectedSaysSo()
    {
        SelectBone(2);

        Click(Button(ToolActionKind.DeleteChain));

        Assert.That(Status, Is.EqualTo("SELECT A CHAIN FIRST"));
    }

    [Test]
    public void TheBoneMenuOffersAChain()
    {
        Entity option = Button(ToolActionKind.AddChain);

        Assert.That(option.Parent, Is.EqualTo(Ui.BoneMenu));
        Assert.That(option.GetComponent<Text>().Value, Is.EqualTo("CHAIN"));
    }

    [Test]
    public void ChainsSwingFromTheirBoneInThePreview()
    {
        AddChainOnHead();
        Frame(10);
        XafChainSimulation simulation = Session.GetComponent<AnimatorChains>().Simulation;
        Assert.That(simulation.Points(0), Has.Count.EqualTo(7));

        Vector2 anchor = simulation.Points(0)[0];
        XafPackage package = Package;
        package.Skeleton.Bones[0].Rest = new Transform2D(0f, new Vector2(60, 0));
        Session.GetComponent<AnimatorModes>().Rest = true;
        Frame(30);

        Assert.That(simulation.Points(0)[0].X, Is.EqualTo(anchor.X + 60f).Within(0.01f));
    }

    [Test]
    public void LoadingADocumentStartsItsChainsHanging()
    {
        AddChainOnHead();
        Frame(10);
        XafChainSimulation simulation = Session.GetComponent<AnimatorChains>().Simulation;
        Assert.That(simulation.Points(0), Is.Not.Empty);

        AnimatorSession.Replace(Session, new XafPackage());
        Frame();

        Assert.That(simulation.Points(0), Is.Empty);
    }
}
