using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Scenes;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Layout;
using Xanadu.Theme;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.Editor;

/// <summary>
/// The editor header rebuilt with the fluent node API. The chrome tests'
/// assertions pass on it, and the builder never reaches for <c>End</c>,
/// <c>Current</c> or <c>AddComponent</c>: this is what migrating
/// <c>EditorScene.Chrome</c> looks like.
/// </summary>
[TestFixture]
public sealed class EditorHeaderFluentTests : EcsSystemTest
{
    private static readonly Vector2 Screen = new(1920, 1080);
    private const float Control = EditorShellMetrics.HeaderControlHeight;
    private const float Pad = (EditorShellMetrics.HeaderHeight - Control) / 2;
    private const float InfoWidth = 420;
    private const string IconDir = "res://Assets/Icons/";

    private UiNode _level;
    private UiNode _song;

    protected override void OnEcsSetUp()
    {
        UiBuilder.DefaultVisuals = new FakeVisualRidFactory();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new UiAnchorSystem());

        Entity parent = Store.CreateEntity();
        BuildHeader(parent);
        Frame(6);
    }

    [TearDown]
    public void TearDown() => UiBuilder.DefaultVisuals = null;

    /// <summary>The same header the editor builds, without the parameter soup.</summary>
    private void BuildHeader(Entity parent)
    {
        UiNode header = UiNode.Layer(Store, parent, 960)
            .Page(Screen)
            .Row()
            .Size(Screen.X, EditorShellMetrics.HeaderHeight);

        UiNode left = header
            .Edge(UiEdge.Left, EditorShellMetrics.HeaderGap)
            .Pad(Pad);
        left.Column()
            .Size(EditorShellMetrics.NameWidth, Control)
            .Fill(SceneUi.PanelColor)
            .Text("LEVEL EDITOR", UiTheme.HeadingFontSize, SceneUi.AccentColor);
        left.IconButton("UN", IconDir + "undo.svg", Control, SceneUi.ButtonColor)
            .With(new EditorButton(EditorButtonKind.Undo));
        left.IconButton("RE", IconDir + "redo.svg", Control, SceneUi.ButtonColor)
            .With(new EditorButton(EditorButtonKind.Redo));

        UiNode info = left
            .Column()
            .Size(InfoWidth, Control)
            .Cross(StackCrossAlignment.Start);
        _level = info.Text("Untitled", 22, SceneUi.AccentColor);
        _song = info.Text("Song: No song loaded", 14, Palette.TextMuted);

        UiNode right = header
            .Edge(UiEdge.Right, EditorShellMetrics.HeaderGap)
            .Pad(Pad);
        right.Button("FILE ▾", UiTheme.FontSize, SceneUi.MutedButtonColor)
            .Size(EditorShellMetrics.DropdownWidth, Control);
        right.Button("SONG ▾", UiTheme.FontSize, SceneUi.MutedButtonColor)
            .Size(EditorShellMetrics.DropdownWidth, Control);
        right.Button("MENU ▾", UiTheme.FontSize, SceneUi.MutedButtonColor)
            .Size(EditorShellMetrics.SettingsWidth, Control);
    }

    private static Rect2 Area(Entity entity)
    {
        Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
        Vector2 center = UiTransform.WorldPosition(
            entity,
            entity.GetComponent<Position>().Value);
        return new Rect2(center + bounds.Position, bounds.Size);
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

        Assert.That(undo.Position.X,
            Is.GreaterThanOrEqualTo(EditorShellMetrics.NameWidth));
        Assert.That(undo.End.Y,
            Is.LessThanOrEqualTo(EditorShellMetrics.HeaderHeight));
        Assert.That(redo.Position.X, Is.GreaterThan(undo.End.X));
        Assert.That(redo.Position.Y, Is.EqualTo(undo.Position.Y).Within(0.5f));
    }

    [Test]
    public void TheHeaderDropdownsHugTheRightEdgeAndTheNameHugsTheLeft()
    {
        Rect2 file = Area(ByText("FILE ▾"));
        Rect2 menu = Area(ByText("MENU ▾"));
        Rect2 name = Area(ByText("LEVEL EDITOR").Parent);

        Assert.That(menu.End.X, Is.EqualTo(Screen.X - Pad).Within(0.5f));
        Assert.That(file.End.X, Is.LessThan(menu.Position.X));
        Assert.That(name.Position.X, Is.EqualTo(Pad).Within(0.5f));
        Assert.That(file.Position.Y, Is.EqualTo(menu.Position.Y).Within(0.5f));
    }

    [Test]
    public void HudLabelsComeBackAsHandlesWithoutBeingSearchedFor()
    {
        Assert.That(_level.IsNull, Is.False);
        Assert.That(_song.IsNull, Is.False);
        Assert.That(_level.Entity.GetComponent<Rectangle>().Bounds.Size.X,
            Is.GreaterThan(0));

        _level.SetText("Night Drive");
        Assert.That(_level.Entity.GetComponent<Text>().Value,
            Is.EqualTo("Night Drive"));
    }
}
