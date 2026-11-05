using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Systems.Editor;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tests.Editor;

/// <summary>Step 4 of docs/render-architecture.md: the editor's layers panel, layer and z steppers, and moving through a layer.</summary>
[TestFixture]
public sealed class EditorLayersTests
{
    private static readonly Vector2 Spot = new(100, 100);

    private EntityStore _store = null!;
    private EditorHistory _history = null!;
    private SystemRoot _root = null!;
    private Entity _editor;
    private EditorLayersPanel _panel;

    [SetUp]
    public void SetUp()
    {
        _store = new EntityStore();
        _history = new EditorHistory(_store);
        _root = new SystemRoot(_store);
        _root.Add(new EditorLayerInspectorSystem(_history));
        _root.Add(new EditorLayersPanelSystem(_history));

        var rows = new EditorLayerRowUi[EditorLayersLayout.MaxRows];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = new EditorLayerRowUi(
                _store.CreateEntity(),
                _store.CreateEntity(new Text("")),
                _store.CreateEntity(),
                _store.CreateEntity(),
                _store.CreateEntity(),
                _store.CreateEntity());
        }

        _panel = new EditorLayersPanel(
            _store.CreateEntity(),
            _store.CreateEntity(),
            rows,
            _store.CreateEntity(),
            _store.CreateEntity(),
            _store.CreateEntity(),
            _store.CreateEntity(),
            _store.CreateEntity(new TextField("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")),
            _store.CreateEntity(new Text("")));
        _editor = _store.CreateEntity(
            new Playfield(1000, 1000),
            new Playhead(0, 120, 0, false),
            new PlayfieldView(Vector2.Zero, 1f, new Vector2(1000, 1000)),
            new NoteSelection(default),
            EditorLayers.Create(),
            LevelLayers.Create(),
            _panel,
            new EditorLayerInspector(_store.CreateEntity(), _store.CreateEntity(new Text("")), _store.CreateEntity(new Text("")), _store.CreateEntity(new Text(""))));
    }

    private void Frame() => _root.Update(new UpdateTick(1f / 60f, 0));

    private void Press(Entity button)
    {
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }

    private void Press(EditorButtonKind kind, int arg)
    {
        Entity button = _store.CreateEntity(new EditorButton(kind, arg));
        Press(button);
        button.DeleteEntity();
    }

    private void OpenPanel() => Press(_panel.Toggle);

    private ref EditorLayers Layers => ref _editor.GetComponent<EditorLayers>();

    private ref LevelLayers Table => ref _editor.GetComponent<LevelLayers>();

    private Entity Block(int layer, int z)
    {
        Entity block = EditorItems.Create(_store, new Block(Spot, new Vector2(50, 50)));
        ItemLayer.Put(block, new ItemLayer(layer, z));
        return block;
    }

    private void Select(params Entity[] items) => _editor.GetComponent<NoteSelection>().SetMany(items);

    private static ItemLayer Of(Entity item) => item.GetComponent<ItemLayer>();

    /// <summary>The panel row slot showing a layer, once the panel is open.</summary>
    private EditorLayerRowUi RowFor(int layer)
    {
        int at = EditorLayerEdits.Rows(_store, Layers, Table).FindIndex(row => row.Layer == layer);
        return _panel.Rows[at - Layers.Scroll];
    }

    [Test]
    public void ThePassRowCyclesTheCurrentLayersPass()
    {
        Entity block = Block(-2, 0);
        Select(block);
        OpenPanel();
        Press(RowFor(-2).Label);
        Assert.That(Layers.Current, Is.EqualTo(-2));

        Press(_panel.Pass);
        Assert.That(Table.Layers[-2].Pass, Is.EqualTo(LayerPassKind.BackgroundReader));

        Press(_panel.Pass);
        Assert.That(Table.Layers[-2].Pass, Is.EqualTo(LayerPassKind.SelfContained));

        Press(_panel.Pass);
        Assert.That(Table.Layers.ContainsKey(-2), Is.False, "a plain layer has no table entry");
    }

    [Test]
    public void RowsListTheLayersInUseFrontFirstWithTheWedge()
    {
        Block(-2, 0);
        Block(-2, 1);
        Block(3, 0);
        Layers.Kept.Add(-5);

        var rows = EditorLayerEdits.Rows(_store, Layers, Table);

        Assert.That(rows.Select(row => row.Layer), Is.EqualTo(new[] { 3, 0, -2, -5 }));
        Assert.That(rows.Select(row => row.Count), Is.EqualTo(new[] { 1, 0, 2, 0 }));
        Assert.That(rows[1].IsWedge, Is.True);
    }

    [Test]
    public void AddingALayerUsesAFreeNumberBetweenTheNeighbours()
    {
        Entity back = Block(1, 0);
        Entity front = Block(3, 0);

        int added = EditorLayerEdits.Insert(_store, ref Layers, ref Table, 1, front: true);

        Assert.That(added, Is.EqualTo(2));
        Assert.That((Of(back).Layer, Of(front).Layer), Is.EqualTo((1, 3)), "nothing had to move");
        Assert.That(Layers.Current, Is.EqualTo(2));
        Assert.That(EditorLayerEdits.Rows(_store, Layers, Table).Select(row => row.Layer), Does.Contain(2));
    }

    [Test]
    public void AddingALayerWithNoRoomPushesTheFarSideAwayFromTheWedge()
    {
        Entity one = Block(1, 0);
        Entity two = Block(2, 0);
        Entity behind = Block(-1, 0);
        Entity further = Block(-2, 0);
        Table.SetName(2, "FRONT");
        Layers.Hidden.Add(-2);

        Assert.That(EditorLayerEdits.Insert(_store, ref Layers, ref Table, 1, front: true), Is.EqualTo(2));
        Assert.That((Of(one).Layer, Of(two).Layer), Is.EqualTo((1, 3)));
        Assert.That(Table.NameOf(3), Is.EqualTo("FRONT"), "the name moves with its layer");

        // In front of -2 is towards the wedge: the new layer takes -2 and pushes it out.
        Assert.That(EditorLayerEdits.Insert(_store, ref Layers, ref Table, -2, front: true), Is.EqualTo(-2));
        Assert.That((Of(behind).Layer, Of(further).Layer), Is.EqualTo((-1, -3)));
        Assert.That(Layers.Hidden, Is.EquivalentTo(new[] { -3 }));
    }

    [Test]
    public void AddingBesideTheWedgeMakesLayerOne()
    {
        Entity one = Block(1, 0);

        Assert.That(EditorLayerEdits.Insert(_store, ref Layers, ref Table, 0, front: true), Is.EqualTo(1));
        Assert.That(Of(one).Layer, Is.EqualTo(2));
        Assert.That(EditorLayerEdits.Insert(_store, ref Layers, ref Table, 0, front: false), Is.EqualTo(-1));
    }

    [Test]
    public void AddingPastTheEndOfTheRangeIsRefused()
    {
        Entity last = Block(200, 0);
        Block(199, 0);

        Assert.That(EditorLayerEdits.Insert(_store, ref Layers, ref Table, 200, front: true), Is.Zero);
        Assert.That(EditorLayerEdits.Insert(_store, ref Layers, ref Table, 200, front: false), Is.Zero);
        Assert.That(Of(last).Layer, Is.EqualTo(200));
    }

    [Test]
    public void SteppingALayerSkipsTheWedge()
    {
        Assert.That(EditorLayerEdits.StepLayer(-1, 1), Is.EqualTo(1));
        Assert.That(EditorLayerEdits.StepLayer(1, -1), Is.EqualTo(-1));
        Assert.That(EditorLayerEdits.StepLayer(200, 1), Is.EqualTo(200));
    }

    [Test]
    public void MovingForwardSwapsZWithTheNextItemOnTheLayer()
    {
        Entity back = Block(-2, 0);
        Entity front = Block(-2, 5);
        Entity other = Block(-1, 0);

        Assert.That(EditorLayerEdits.Move(_store, [back], 1), Is.True);
        Assert.That((Of(back).Z, Of(front).Z), Is.EqualTo((5, 0)));

        // Now front-most on its layer: the item on the next layer is a wall.
        Assert.That(EditorLayerEdits.Move(_store, [back], 1), Is.False);
        Assert.That(Of(back).Layer, Is.EqualTo(-2));
        Assert.That(Of(other), Is.EqualTo(new ItemLayer(-1, 0)));
    }

    [Test]
    public void MovingASelectionKeepsItsOwnOrder()
    {
        Entity a = Block(-2, 0);
        Entity b = Block(-2, 1);
        Entity c = Block(-2, 2);

        EditorLayerEdits.Move(_store, [a, b], 1);

        Assert.That(EditorLayerEdits.FrontFirst(_store), Is.EqualTo(new[] { b, a, c }));
    }

    [Test]
    public void TheMoveCommandIsOneUndoStep()
    {
        Entity back = Block(-2, 0);
        Block(-2, 1);
        Select(back);
        var commands = new EditorLayerCommands(new EditorCommandContext(_store, _history));

        commands.TryApply(EditorCommand.MoveInLayer(1), _editor);

        Assert.That(Of(back).Z, Is.EqualTo(1));
        Assert.That(_history.Undo(), Is.True);
        Assert.That(_store.Query<Block>().ToEntityList().Select(block => Of(block).Z).Order(), Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void TheLayerStepperPutsTheSelectionOnTheNextLayerInFront()
    {
        Block(1, 4);
        Entity moving = Block(-1, 0);
        Select(moving);

        Press(EditorButtonKind.LayerStep, 1);

        Assert.That(Of(moving), Is.EqualTo(new ItemLayer(1, 5)), "past the wedge, on top of what's there");
        Assert.That(_history.CanUndo, Is.True);
    }

    [Test]
    public void TheLayerStepperRefusesToBreakABufferedGroup()
    {
        LevelGroups groups = LevelGroups.Create();
        groups.Groups[0].Buffered = true;
        _editor.AddComponent(groups);
        Entity member = Block(-2, 0);
        member.AddComponent(new ItemGroups(ItemGroups.Bit(0)));
        Entity others = Block(-2, 1);
        others.AddComponent(new ItemGroups(ItemGroups.Bit(0)));
        Select(member);

        Press(EditorButtonKind.LayerStep, 1);

        Assert.That(Of(member).Layer, Is.EqualTo(-2), "the group stays on one layer");
        Assert.That(_history.CanUndo, Is.False, "nothing was edited");
        Assert.That(_editor.HasComponent<WarningRequest>(), Is.True);
        Assert.That(
            _editor.GetComponent<WarningRequest>().Message,
            Is.EqualTo(GroupBufferRules.OneLayer));
    }

    [Test]
    public void TheZStepperRaisesTheSelection()
    {
        Entity a = Block(-2, 0);
        Entity b = Block(-2, 3);
        Select(a, b);

        Press(EditorButtonKind.ZStep, 1);

        Assert.That((Of(a).Z, Of(b).Z), Is.EqualTo((1, 4)));
    }

    [Test]
    public void HidingALayerStopsItDrawingAndDeselectsIt()
    {
        Entity block = Block(-2, 0);
        Select(block);
        OpenPanel();

        Press(RowFor(-2).Hide);

        Assert.That(Layers.Shows(-2), Is.False);
        Assert.That(Layers.Shows(0), Is.True, "the wedge always draws");
        Assert.That(_editor.GetComponent<NoteSelection>().AllNotes, Is.Empty);
    }

    [Test]
    public void ALockedLayerCannotBePicked()
    {
        Entity block = Block(-2, 0);
        var picker = new EditorItemPicker(new EditorSceneQuery(_store));
        OpenPanel();
        Assert.That(picker.FindItemAt(Spot + new Vector2(10, 10), 0), Is.EqualTo(block));

        Press(RowFor(-2).Lock);

        Assert.That(picker.FindItemAt(Spot + new Vector2(10, 10), 0).IsNull, Is.True);
        Assert.That(picker.FindNotesIn(new Rect2(0, 0, 1000, 1000), 0), Is.Empty);
        Assert.That(Layers.Shows(-2), Is.True, "locked still draws");
    }

    [Test]
    public void SoloShowsOnlyThatLayer()
    {
        Block(-2, 0);
        Block(1, 0);
        OpenPanel();

        Press(RowFor(1).Solo);

        Assert.That((Layers.Shows(1), Layers.Shows(-2)), Is.EqualTo((true, false)));
        Press(RowFor(1).Solo);
        Assert.That(Layers.Shows(-2), Is.True);
    }

    [Test]
    public void SelectSelectsEverythingOnTheLayer()
    {
        Entity a = Block(-2, 0);
        Entity b = Block(-2, 1);
        Block(1, 0);
        OpenPanel();

        Press(RowFor(-2).Select);

        Assert.That(_editor.GetComponent<NoteSelection>().AllNotes, Is.EquivalentTo(new[] { a, b }));
    }

    [Test]
    public void FrontAddsALayerBesideTheCurrentRowAndNameRenamesIt()
    {
        Entity block = Block(-1, 0);
        OpenPanel();
        Press(RowFor(-1).Label);

        Press(_panel.Front);

        // In front of -1 is between it and the wedge: the new layer is -1 and the old one steps back.
        Assert.That(Layers.Current, Is.EqualTo(-1));
        Assert.That(Of(block).Layer, Is.EqualTo(-2));
        Assert.That(_history.Undo(), Is.True, "moving the items was one undo step");

        _panel.Name.AddTag<TextFieldFocused>();
        _panel.Name.GetComponent<TextField>().Value = "GLOW";
        Frame();
        _panel.Name.RemoveTag<TextFieldFocused>();
        Frame();

        Assert.That(Table.NameOf(-1), Is.EqualTo("GLOW"));
        Assert.That(EditorLayersPanelSystem.Label(new EditorLayerRow(-1, 0), Table.NameOf(-1)), Does.Contain("GLOW"));
        Assert.That(_history.Undo(), Is.True, "leaving the field made the rename one undo step");
        Assert.That(Table.NameOf(-1), Is.Empty);
    }

    [Test]
    public void TheWedgeRowCannotBeHidden()
    {
        OpenPanel();

        Press(RowFor(0).Hide);

        Assert.That(Layers.Hidden, Is.Empty);
        Assert.That(EditorLayersPanelSystem.Label(new EditorLayerRow(0, 0), ""), Does.Contain("PLAYER"));
    }
}
