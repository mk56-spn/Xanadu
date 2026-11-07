using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

[TestFixture]
public sealed class EditorToolGroupSystemTests : EcsSystemTest
{
    private static readonly int NotesGroup = 0;
    private static readonly int HazardGroup = 3;

    private Entity _editor;
    private Entity _notes;
    private Entity _hazards;
    private Entity _mines;

    protected override void OnEcsSetUp()
    {
        _editor = Store.CreateEntity(new EditorPlacement { Mode = EditorPlacementMode.Blocks });
        _notes = Group(NotesGroup, out _);
        _hazards = Group(HazardGroup, out Entity hazardFlyout);
        _mines = Store.CreateEntity(EditorButton.Place(EditorPlacementMode.Mines), new Text(""));
        hazardFlyout.AddChild(_mines);

        Root.Add(new EditorPlacementSystem(Store));
        Root.Add(new EditorToolGroupSystem(Store));
        Frame();
    }

    private Entity Group(int index, out Entity flyout)
    {
        flyout = Store.CreateEntity();
        Entity icon = Store.CreateEntity(new Text(""));
        Entity button = Store.CreateEntity(new EditorButton(EditorButtonKind.ToolGroup, index));
        button.AddComponent(new EditorToolGroup(index, flyout, icon));
        return button;
    }

    private void Press(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }

    private static bool IsOpen(Entity group) => group.GetComponent<EditorToolGroup>().Open;

    [Test]
    public void TheButtonTogglesItsFlyout()
    {
        Press(_hazards);
        Assert.That(IsOpen(_hazards), Is.True);
        Assert.That(UiLayout.IsHidden(_hazards.GetComponent<EditorToolGroup>().Flyout), Is.False);

        Press(_hazards);
        Assert.That(IsOpen(_hazards), Is.False);
    }

    [Test]
    public void OpeningOneGroupClosesTheOther()
    {
        Press(_hazards);
        Press(_notes);

        Assert.That(IsOpen(_notes), Is.True);
        Assert.That(IsOpen(_hazards), Is.False);
    }

    [Test]
    public void PickingAToolFromTheFlyoutKeepsItOpenAndLightsTheGroup()
    {
        Press(_hazards);
        Press(_mines);

        Assert.That(_editor.GetComponent<EditorPlacement>().Mode, Is.EqualTo(EditorPlacementMode.Mines));
        Assert.That(IsOpen(_hazards), Is.True);
        Assert.That(
            _hazards.GetComponent<EditorToolGroup>().Icon.GetComponent<Text>().Value,
            Is.EqualTo("✹"),
            "the group shows the tool it is on");
    }

    [Test]
    public void AToolOutsideTheGroupClosesAnOpenFlyout()
    {
        Press(_notes);
        _editor.GetComponent<EditorPlacement>().Mode = EditorPlacementMode.Spikes;
        Frame();

        Assert.That(IsOpen(_notes), Is.False);
        Assert.That(_hazards.GetComponent<EditorToolGroup>().Icon.GetComponent<Text>().Value, Is.EqualTo("▲"));
    }

    [Test]
    public void StartPointsShareAGroupWithCharacterSwitches()
    {
        EditorToolGroupInfo group = System.Array.Find(
            EditorToolGroups.All,
            g => EditorToolGroups.Holds(g, EditorPlacementMode.Start));

        Assert.That(EditorToolGroups.Holds(group, EditorPlacementMode.CharacterSwitch), Is.True);
    }
}
