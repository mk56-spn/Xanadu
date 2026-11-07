using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorToolSectionSystemTests : EcsSystemTest
{
    private Entity _editor;
    private Entity _panel;
    private Entity _blockSize;
    private Entity _strength;
    private Entity _cost;
    private Entity _shape;

    protected override void OnEcsSetUp()
    {
        _panel = Store.CreateEntity();
        _blockSize = Store.CreateEntity(new EditorToolSection(EditorToolSections.BlockSize));
        _strength = Store.CreateEntity(new EditorToolSection(EditorToolSections.MoveStrength));
        _cost = Store.CreateEntity(new EditorToolSection(EditorToolSections.ContactCost));
        _shape = Store.CreateEntity(new EditorToolSection(EditorToolSections.FormShape));
        _editor = Store.CreateEntity(
            new EditorPlacement { Mode = EditorPlacementMode.Notes },
            new EditorLeftMenu(_panel));
        Root.Add(new EditorToolSectionSystem(Store));
    }

    private bool Hidden(Entity entity) => entity.Tags.Has<UiHidden>();

    [Test]
    public void NotesShowNoSectionAndHideThePanel()
    {
        Frame();

        Assert.That(Hidden(_panel), Is.True);
        Assert.That(Hidden(_blockSize), Is.True);
        Assert.That(Hidden(_strength), Is.True);
        Assert.That(Hidden(_cost), Is.True);
        Assert.That(Hidden(_shape), Is.True);
    }

    [Test]
    public void BlocksShowTheirSizeAndShape()
    {
        _editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Blocks;

        Frame();

        Assert.That(Hidden(_blockSize), Is.False);
        Assert.That(Hidden(_shape), Is.False);
        Assert.That(Hidden(_panel), Is.False);
    }

    [TestCase(EditorPlacementMode.Dash, false, true, true)]
    [TestCase(EditorPlacementMode.Momentum, false, true, true)]
    [TestCase(EditorPlacementMode.Contact, true, false, true)]
    [TestCase(EditorPlacementMode.FormStep, true, true, false)]
    public void EachToolShowsItsOwnSection(
        EditorPlacementMode mode,
        bool strengthHidden,
        bool costHidden,
        bool shapeHidden)
    {
        _editor.GetComponent<EditorPlacement>().Mode = mode;

        Frame();

        Assert.That(Hidden(_strength), Is.EqualTo(strengthHidden));
        Assert.That(Hidden(_cost), Is.EqualTo(costHidden));
        Assert.That(Hidden(_shape), Is.EqualTo(shapeHidden));
    }

    [Test]
    public void SpikesHideTheWholePanelAndItComesBack()
    {
        _editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Spikes;
        Frame();
        Assert.That(Hidden(_panel), Is.True);

        _editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Blocks;
        Frame();
        Assert.That(Hidden(_panel), Is.False);
        Assert.That(Hidden(_blockSize), Is.False);
    }
}
