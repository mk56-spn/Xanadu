using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Scenes;
using Xanadu.Systems.Layout;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
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
        // The strip holds the lone tools and the group buttons; grouped tools sit in flyouts.
        var tools = new System.Collections.Generic.List<Rect2>();
        foreach (Entity entity in Store.Query<EditorButton>().ToEntityList())
        {
            EditorButton button = entity.GetComponent<EditorButton>();
            bool inStrip = button.Kind == EditorButtonKind.ToolGroup ||
                           (button.Kind == EditorButtonKind.Place &&
                            Area(entity).Position.X < EditorShellMetrics.ToolStripWidth);
            if (inStrip)
                tools.Add(Area(entity));
        }
        tools.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));

        Assert.That(tools, Has.Count.EqualTo(EditorToolGroups.All.Length));
        // Centred in the strip; with more tools than fit at full size they shrink.
        Assert.That(
            tools[0].Position.X,
            Is.EqualTo((EditorShellMetrics.ToolStripWidth - tools[0].Size.X) / 2).Within(0.5f));
        Assert.That(tools[^1].End.Y, Is.LessThanOrEqualTo(Area(SidebarPanel()).End.Y + 0.5f), "the last tool stays above the footer");
        Assert.That(tools[0].Position.Y, Is.GreaterThanOrEqualTo(EditorShellMetrics.HeaderHeight));
        for (int i = 1; i < tools.Count; i++)
        {
            Assert.That(tools[i].Position.Y - tools[i - 1].End.Y,
                Is.EqualTo(EditorShellMetrics.ToolGap).Within(0.5f));
            Assert.That(tools[i].Position.X, Is.EqualTo(tools[0].Position.X).Within(0.5f));
        }
    }

    [Test]
    public void EveryToolHasExactlyOneButton()
    {
        var modes = new System.Collections.Generic.List<EditorPlacementMode>();
        foreach (Entity entity in EditorButton.All(Store, EditorButtonKind.Place))
            modes.Add((EditorPlacementMode)entity.GetComponent<EditorButton>().Arg);

        Assert.That(modes, Is.Unique);
        Assert.That(modes, Is.EquivalentTo(System.Enum.GetValues<EditorPlacementMode>()));
    }

    [Test]
    public void AGroupsFlyoutOpensBesideItsButton()
    {
        foreach (Entity entity in Store.Query<EditorToolGroup>().ToEntityList())
        {
            EditorToolGroup group = entity.GetComponent<EditorToolGroup>();
            Assert.That(UiLayout.IsHidden(group.Flyout), Is.True, "closed at first");

            Rect2 button = Area(entity);
            Rect2 flyout = Area(group.Flyout);
            Assert.That(flyout.Position.X, Is.GreaterThanOrEqualTo(EditorShellMetrics.ToolStripWidth), "right of the strip");
            Assert.That(flyout.GetCenter().Y, Is.EqualTo(button.GetCenter().Y).Within(1f), "level with its button");
        }
    }

    [Test]
    public void EachKeyedToolShowsItsKeyInsideItsButton()
    {
        // Grouped tools are laid out once their flyout opens.
        foreach (Entity group in Store.Query<EditorToolGroup>().ToEntityList())
            UiLayout.SetHidden(group.GetComponent<EditorToolGroup>().Flyout, false);
        Frame(3);

        foreach (Entity entity in EditorButton.All(Store, EditorButtonKind.Place))
        {
            var mode = (EditorPlacementMode)entity.GetComponent<EditorButton>().Arg;
            string key = Xanadu.Editor.EditorPlacementKeybinds.LabelFor(mode);
            Assert.That(entity.GetComponent<UiTooltip>().Text, Does.EndWith(key.Length > 0 ? $"({key})" : ""));
            if (key.Length == 0)
                continue;

            Entity label = default;
            foreach (Entity sibling in entity.ChildEntities)
            {
                if (sibling.TryGetComponent(out Text text) && text.Value == key)
                    label = sibling;
            }

            Assert.That(label.IsNull, Is.False, $"{mode} has no key label");
            Rect2 button = Area(entity);
            Rect2 area = Area(label);
            Assert.That(button.Encloses(area), Is.True, $"{mode}: {area} is outside {button}");
            Assert.That(area.GetCenter().X, Is.GreaterThan(button.GetCenter().X), $"{mode}: the key sits right");
            Assert.That(area.GetCenter().Y, Is.GreaterThan(button.GetCenter().Y), $"{mode}: the key sits low");
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

    [Test]
    public void TheClockSitsInTheFooterLeftOfACentredTimeline()
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, Screen);
        Rect2 ruler = EditorLayout.RulerRect(view);
        Rect2 clock = Area(ByText("00:00.000 / 00:00.000").Parent);

        Assert.That(clock.End.X, Is.LessThanOrEqualTo(ruler.Position.X));
        Assert.That(clock.GetCenter().Y, Is.EqualTo(ruler.GetCenter().Y).Within(1f));
        Assert.That(ruler.Size.Y, Is.EqualTo(EditorLayout.TimelineHeight));
    }

    [Test]
    public void TheFooterPanelStartsAtTheTransportWithNoGap()
    {
        var view = new PlayfieldView(Vector2.Zero, 1f, Screen);
        Rect2 footer = EditorLayout.FooterRect(view);

        Assert.That(footer.Position.Y, Is.EqualTo(_shell.Footer.Position.Y).Within(0.5f));
        Assert.That(EditorLayout.TransportRect(view).Position.Y, Is.EqualTo(footer.Position.Y));
        Assert.That(EditorLayout.AnalysisRect(view).Position.Y,
            Is.GreaterThanOrEqualTo(EditorLayout.TransportRect(view).End.Y));
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
    public void TheHeaderMenuHugsTheRightEdgeAndTheNameHugsTheLeft()
    {
        Rect2 menu = Area(ByText("MENU ▾"));
        Rect2 name = Area(ByText("LEVEL EDITOR").Parent);

        Assert.That(menu.End.X, Is.EqualTo(Screen.X - HeaderPad).Within(0.5f));
        Assert.That(name.Position.X, Is.EqualTo(HeaderPad).Within(0.5f));
    }

    [Test]
    public void TheMenuHoldsSaveLevelInfoAndExitOnly()
    {
        var labels = new System.Collections.Generic.List<string>();
        foreach (Entity option in Store.Query<DropdownOption>().WithDisabled().ToEntityList())
        {
            if (option.GetComponent<DropdownOption>().Owner == ByText("MENU ▾"))
                labels.Add(option.GetComponent<Text>().Value);
        }

        Assert.That(labels, Is.EquivalentTo(new[] { "SAVE", "LEVEL INFO", "EXIT" }));
        Assert.Throws<AssertionException>(() => ByText("FILE ▾"));
        Assert.Throws<AssertionException>(() => ByText("SONG ▾"));
    }

    [Test]
    public void TheSidebarControlsFlowInsideItInOrder()
    {
        Rect2 panel = Area(SidebarPanel());
        Rect2 firstCell = Area(Cell(0));
        Rect2 lastCell = Area(Cell(15));

        Assert.That(panel.Encloses(firstCell), Is.True);
        Assert.That(panel.Encloses(lastCell), Is.True);
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
        Assert.That(second.Position.X - first.End.X, Is.EqualTo(Xanadu.Systems.Editor.EditorGroupLayout.CellGap).Within(0.5f));
        Assert.That(fifth.Position.X, Is.EqualTo(first.Position.X).Within(0.5f));
        Assert.That(fifth.Position.Y, Is.GreaterThan(first.End.Y));
        Assert.That(last.Position.X, Is.EqualTo(Area(Cell(GroupLimits.Columns - 1)).Position.X).Within(0.5f));
    }

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
    public void TheLeftPanelDocksToTheViewportCorner()
    {
        Rect2 left = Area(Store.Query<EditorLeftMenu>().ToEntityList()[0]
            .GetComponent<EditorLeftMenu>().Panel);

        Assert.That(left.Position.X, Is.EqualTo(_shell.Viewport.Position.X).Within(0.5f));
        Assert.That(left.Position.Y, Is.EqualTo(_shell.Viewport.Position.Y).Within(0.5f));
    }

    [Test]
    public void TheUnsavedFlagSitsInTheHeaderClearOfTheRightHandControls()
    {
        ShellStatusLabels labels = Store.Query<EditorHud>().ToEntityList()[0].GetComponent<EditorHud>().Status;
        Rect2 flag = Area(labels.Flag);
        // MENU is the only control on the header's right.
        Rect2 file = Area(ByText("MENU ▾"));

        Assert.That(_shell.Header.Encloses(flag), Is.True);
        Assert.That(flag.End.X, Is.LessThanOrEqualTo(file.Position.X));
    }

    [Test]
    public void TheTimeAndNoteCountHugTheFootersEdges()
    {
        Rect2 time = Area(ByText("00:00.000 / 00:00.000").Parent);
        Rect2 notes = Area(ByText("0 notes"));

        Assert.That(time.Position.X, Is.EqualTo(EditorLayout.Margin).Within(0.5f));
        Assert.That(notes.End.X, Is.EqualTo(Screen.X - EditorLayout.Margin).Within(0.5f));
        Assert.That(time.Position.Y, Is.GreaterThanOrEqualTo(_shell.Footer.Position.Y - 1f));
    }

    private Entity SidebarPanel()
    {
        foreach (Entity entity in Store.Query<EditorSidebarLayout>().ToEntityList())
            return entity.GetComponent<EditorSidebarLayout>().Panel;
        throw new AssertionException("No sidebar.");
    }
}
