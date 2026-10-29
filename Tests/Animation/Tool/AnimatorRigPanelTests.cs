using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Xanadu.Tools.Animator;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tests.Animation.Tool;

/// <summary>The right panel following the rig: section counts, the EFFECT tab's caption, and tabs that follow the selection.</summary>
[TestFixture]
public sealed class AnimatorRigPanelTests : AnimatorToolTest
{
    protected override void AddSystems()
    {
        Root.Add(new UiTabSystem());
        Root.Add(new UiCollapsibleSystem());
        Root.Add(new AnimatorActionSystem());
        Root.Add(new AnimatorHierarchySystem(new FakeVisualRidFactory()));
        Root.Add(new AnimatorRigPanelSystem());
    }

    private int SelectedTab => Ui.Tabs.GetComponent<UiTabs>().Selected;

    private static string Header(Entity section) =>
        section.GetComponent<UiCollapsible>().Header.GetComponent<Text>().Value;

    private void AddStreakOnTorso()
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);
        Frame();
        Click(Button(ToolActionKind.AddEffect));
        Frame();
    }

    [Test]
    public void SectionHeadersCountWhatTheyHold()
    {
        Frame(2);

        Assert.That(Header(Ui.BoneSection), Does.EndWith($"SKELETON  ·  {Package.Skeleton.Bones.Count}"));
        Assert.That(Header(Ui.EffectSection), Does.EndWith("EFFECTS  ·  0"));

        AddStreakOnTorso();

        Assert.That(Header(Ui.EffectSection), Does.EndWith("EFFECTS  ·  1"));
    }

    [Test]
    public void SelectingAnEffectOpensItsTabAndNamesIt()
    {
        Frame();
        Assert.That(SelectedTab, Is.EqualTo(AnimatorLayout.PoseTab));
        Assert.That(Ui.EffectCaption.GetComponent<Text>().Value, Is.EqualTo("NO EFFECT SELECTED"));

        AddStreakOnTorso();

        Assert.That(SelectedTab, Is.EqualTo(AnimatorLayout.EffectTab));
        Assert.That(Ui.EffectCaption.GetComponent<Text>().Value, Does.StartWith("STREAK  on "));
    }

    [Test]
    public void PickingABoneAfterAnEffectGoesBackToPose()
    {
        AddStreakOnTorso();

        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(0);
        Frame(2);

        Assert.That(SelectedTab, Is.EqualTo(AnimatorLayout.PoseTab));
    }

    [Test]
    public void ATabTheUserPicksStaysPicked()
    {
        Session.GetComponent<AnimatorSelection>().SelectOnlyBone(1);
        Frame();

        Click(Ui.Tabs.GetComponent<UiTabs>().Buttons[AnimatorLayout.FlowTab]);
        Frame(2);

        Assert.That(SelectedTab, Is.EqualTo(AnimatorLayout.FlowTab));
    }
}
