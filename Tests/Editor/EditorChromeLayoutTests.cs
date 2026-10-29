using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Layout;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

/// <summary>Lays out the real editor scene and checks where the chrome ends up.</summary>
[TestFixture]
public sealed class EditorChromeLayoutTests : EcsSystemTest
{
    private const float HeaderPad =
        (EditorShellMetrics.HeaderHeight - EditorShellMetrics.HeaderControlHeight) / 2;

    private static readonly Vector2 Screen = new(1920, 1080);
    private EditorShellRects _shell;

    protected override void OnEcsSetUp()
    {
        UiBuilder.DefaultVisuals = new FakeVisualRidFactory();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiAnchorSystem());
        Entity parent = Store.CreateEntity();
        EditorScene.Build(Store, Screen, parent);
        Frame(6);
        _shell = EditorShell.Compute(Screen, new EditorShellOptions(EditorFooterHeight.Level));
    }

    [TearDown]
    public void TearDown() => UiBuilder.DefaultVisuals = null;

    private static Rect2 Area(Entity entity)
    {
        Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
        Vector2 center = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        return new Rect2(center + bounds.Position, bounds.Size);
    }

    private Entity Button(EditorButtonKind kind)
    {
        foreach (Entity entity in Store.Query<EditorButton>().ToEntityList())
        {
            if (entity.GetComponent<EditorButton>().Kind == kind)
                return entity;
        }
        throw new AssertionException($"No {kind} button.");
    }

    [Test]
    public void UndoAndRedoSitInTheHeaderRightOfTheName()
    {
        Rect2 undo = Area(Button(EditorButtonKind.Undo));
        Rect2 redo = Area(Button(EditorButtonKind.Redo));

        Assert.That(undo.Position.X, Is.GreaterThanOrEqualTo(EditorShellMetrics.NameWidth));
        Assert.That(undo.End.Y, Is.LessThanOrEqualTo(EditorShellMetrics.HeaderHeight));
        Assert.That(redo.Position.X, Is.GreaterThan(undo.End.X));
        Assert.That(redo.Position.Y, Is.EqualTo(undo.Position.Y).Within(0.5f));
    }

    [Test]
    public void TheSidebarIsDockedToTheRightEdgeForTheFullMiddleHeight()
    {
        Rect2 area = Area(SidebarPanel());

        Assert.That(area.End.X, Is.EqualTo(Screen.X).Within(0.5f));
        Assert.That(area.Position.Y, Is.EqualTo(_shell.RightPanel.Position.Y).Within(0.5f));
        Assert.That(area.Size.Y, Is.EqualTo(_shell.RightPanel.Size.Y).Within(0.5f));
        Assert.That(area.Size.X, Is.EqualTo(EditorShellMetrics.RightPanelWidth).Within(0.5f));
    }

    [Test]
    public void ThePlacementToolsFlowDownTheStripFlushToTheLeftEdge()
    {
        var tools = new System.Collections.Generic.List<Rect2>();
        foreach (Entity entity in Store.Query<EditorPlacementButton>().ToEntityList())
            tools.Add(Area(entity));
        tools.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

        Assert.That(tools, Has.Count.EqualTo(8));
        Assert.That(tools[0].Position.X, Is.EqualTo(4f).Within(0.5f));
        Assert.That(tools[0].Position.Y, Is.GreaterThanOrEqualTo(EditorShellMetrics.HeaderHeight));
        for (int i = 1; i < tools.Count; i++)
        {
            Assert.That(tools[i].Position.Y - tools[i - 1].End.Y,
                Is.EqualTo(EditorShellMetrics.ToolGap).Within(0.5f));
            Assert.That(tools[i].Position.X, Is.EqualTo(tools[0].Position.X).Within(0.5f));
        }
    }

    [Test]
    public void TheTransportIsCentredAndThePlayfieldAreaClearsTheStatusLine()
    {
        Rect2 play = Area(Button(EditorButtonKind.Play));
        Rect2 metronome = Area(Button(EditorButtonKind.Metronome));
        float middle = (play.Position.X + metronome.End.X) / 2;

        Assert.That(middle, Is.EqualTo(Screen.X / 2).Within(1f));
        Assert.That(play.Position.Y, Is.GreaterThanOrEqualTo(_shell.Footer.Position.Y - 1f));
    }

    private Entity ByText(string text)
    {
        foreach (Entity entity in Store.Query<Text>().ToEntityList())
        {
            if (entity.GetComponent<Text>().Value == text)
                return entity;
        }
        throw new AssertionException($"No '{text}'.");
    }

    [Test]
    public void TheHeaderDropdownsHugTheRightEdgeAndTheNameHugsTheLeft()
    {
        Rect2 file = Area(ByText("FILE ▾"));
        Rect2 menu = Area(ByText("MENU ▾"));
        Rect2 name = Area(ByText("LEVEL EDITOR").Parent);

        Assert.That(menu.End.X, Is.EqualTo(Screen.X - HeaderPad).Within(0.5f));
        Assert.That(file.End.X, Is.LessThan(menu.Position.X));
        Assert.That(name.Position.X, Is.EqualTo(HeaderPad).Within(0.5f));
        Assert.That(file.Position.Y, Is.EqualTo(menu.Position.Y).Within(0.5f));
    }

    [Test]
    public void TheSidebarControlsFlowInsideItInOrder()
    {
        Rect2 panel = Area(SidebarPanel());
        Entity character = _characterButton();
        Rect2 dropdown = Area(character);
        Rect2 firstCell = Area(Cell(0));
        Rect2 lastCell = Area(Cell(15));

        Assert.That(panel.Encloses(dropdown), Is.True);
        Assert.That(panel.Encloses(firstCell), Is.True);
        Assert.That(panel.Encloses(lastCell), Is.True);
        Assert.That(firstCell.Position.Y, Is.GreaterThan(dropdown.End.Y));
        Assert.That(lastCell.Position.Y, Is.GreaterThan(firstCell.Position.Y));
    }

    [Test]
    public void TheGroupCellsFormAFourByFourGrid()
    {
        Rect2 first = Area(Cell(0));
        Rect2 second = Area(Cell(1));
        Rect2 fifth = Area(Cell(GroupLimits.Columns));
        Rect2 last = Area(Cell(GroupLimits.Count - 1));

        Assert.That(second.Position.Y, Is.EqualTo(first.Position.Y).Within(0.5f));
        Assert.That(second.Position.X - first.End.X, Is.EqualTo(6f).Within(0.5f));
        Assert.That(fifth.Position.X, Is.EqualTo(first.Position.X).Within(0.5f));
        Assert.That(fifth.Position.Y, Is.GreaterThan(first.End.Y));
        Assert.That(last.Position.X, Is.EqualTo(Area(Cell(GroupLimits.Columns - 1)).Position.X).Within(0.5f));
    }

    [Test]
    public void AnOpenCharacterDropdownPushesTheGroupsDown()
    {
        Entity character = _characterButton();
        float before = Area(Cell(0)).Position.Y;

        character.GetComponent<Dropdown>().Open = true;
        character.Parent.AddTag<LayoutDirty>();
        Frame(2);

        Assert.That(Area(Cell(0)).Position.Y, Is.GreaterThan(before + 20f));
    }

    private Entity _characterButton() =>
        Store.Query<EditorSidebarLayout>().ToEntityList()[0]
            .GetComponent<EditorSidebarLayout>().CharacterButton;

    private Entity Cell(int index)
    {
        foreach (Entity entity in Store.Query<EditorGroupCell>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupCell>().Index == index)
                return entity;
        }
        throw new AssertionException($"No cell {index}.");
    }

    [Test]
    public void TheLeftPanelDocksToTheViewportCornerAndTheStatusLineToItsBottom()
    {
        Rect2 left = Area(Store.Query<EditorLeftMenu>().ToEntityList()[0]
            .GetComponent<EditorLeftMenu>().Panel);
        Rect2 hint = Area(ByText(
            "Space play   Home stop   Delete note   ←/→ nudge   Ctrl+Z/Y undo   T tap tempo   Target: select a note").Parent);

        Assert.That(left.Position.X, Is.EqualTo(_shell.Viewport.Position.X).Within(0.5f));
        Assert.That(left.Position.Y, Is.EqualTo(_shell.Viewport.Position.Y).Within(0.5f));
        Assert.That(hint.Position.X, Is.EqualTo(_shell.StatusLine.Position.X).Within(0.5f));
        Assert.That(hint.End.Y, Is.EqualTo(_shell.StatusLine.End.Y).Within(0.5f));
        Assert.That(hint.Size.X, Is.EqualTo(_shell.StatusLine.Size.X).Within(0.5f));
    }

    [Test]
    public void TheTimeAndNoteCountHugTheFootersEdges()
    {
        Rect2 time = Area(ByText("00:00.000 / 00:00.000").Parent);
        Rect2 notes = Area(ByText("0 notes"));

        Assert.That(time.Position.X, Is.EqualTo(15f).Within(0.5f));
        Assert.That(notes.End.X, Is.EqualTo(Screen.X - 15f).Within(0.5f));
        Assert.That(time.Position.Y, Is.GreaterThanOrEqualTo(_shell.Footer.Position.Y - 1f));
    }

    private Entity SidebarPanel()
    {
        foreach (Entity entity in Store.Query<EditorSidebarLayout>().ToEntityList())
            return entity.GetComponent<EditorSidebarLayout>().Panel;
        throw new AssertionException("No sidebar.");
    }
}
