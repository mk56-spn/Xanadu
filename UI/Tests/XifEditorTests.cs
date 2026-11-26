using System.Linq;
using Xanadu.Tests.Support.Fakes;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using NUnit.Framework;
using Xanadu.Core;
using Xanadu.Core.Items;
using Xanadu.Tests.Support;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Layout;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Tests;

/// <summary>
/// The shared XIF editor against a fake host, as any tool would host it:
/// shown only with an item, every edit going through the host, points
/// dragged in the shape box as one gesture.
/// </summary>
[TestFixture]
public sealed class XifEditorTests : EcsSystemTest
{
    private static readonly Vector2 Home = new(400, 300);

    private FakePointer _pointer = null!;
    private FakeHost _host = null!;
    private Entity _panel;

    private XifEditor Editor => _panel.GetComponent<XifEditor>();
    private XifPolygon Polygon => _host.Current!.Polygons[Editor.Polygon];

    protected override void OnEcsSetUp()
    {
        _pointer = new FakePointer();
        _host = new FakeHost();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new XifEditorSystem(_pointer, new FakeVisualRidFactory()));
        UiNode layer = UiNode.Layer(Store, default, 5, new FakeVisualRidFactory());
        _panel = XifEditorPanel.Build(layer, new Rect2(Home - new Vector2(300, 250), new Vector2(600, 500)), new Vector2(1920, 1080),
            FakeHost.Shaders, []);
        _host.Listen(_panel);
        XifEditorSystem.Open(_panel);
        Frame(2);
    }

    private Entity ButtonOf(XifEditorActionKind kind)
    {
        foreach ((Entity entity, XifEditorActionKind buttonKind, _) in UiCommand.All<XifEditorActionKind>(Store))
        {
            if (buttonKind == kind)
                return entity;
        }
        throw new InvalidOperationException(kind.ToString());
    }

    private void Click(XifEditorActionKind kind)
    {
        Entity button = ButtonOf(kind);
        button.AddTag<Pressed>();
        Frame();
        button.RemoveTag<Pressed>();
        Frame();
    }

    private void Type(XifEditorFieldKind kind, string value)
    {
        Entity field = default;
        foreach (Entity entity in Store.Query<XifEditorField>().ToEntityList())
        {
            if (entity.GetComponent<XifEditorField>().Kind == kind)
                field = entity;
        }

        field.AddTag<TextFieldFocused>();
        Frame();
        field.GetComponent<TextField>().Value = value;
        field.RemoveTag<TextFieldFocused>();
        Frame();
    }

    private string Text(Entity entity) => entity.GetComponent<Text>().Value;

    private Vector2 PointOnScreen(int index)
    {
        Rect2 box = XifEditorSystem.Area(Editor.Parts.Canvas);
        return XifEditorView.ToScreen(Editor.View, box) * Polygon.Points[index];
    }

    [Test]
    public void ThePanelShowsOnlyWhileTheHostHasAnItem()
    {
        Assert.That(_panel.GetComponent<Position>().Value, Is.EqualTo(Home));

        _host.Current = null;
        Frame();

        Assert.That(UiLayout.IsHidden(_panel), Is.True);
    }

    [Test]
    public void ItsReadoutsDescribeTheItem()
    {
        Assert.That(Text(Editor.Parts.Title), Is.EqualTo("EDIT XIF · STREAK ITEM"));
        Assert.That(Text(Editor.Parts.PointLabel), Is.EqualTo("1/4  -16, -16"));
        Assert.That(Text(UiTabbedPickers.ButtonOf(Editor.Parts.Shader)), Is.EqualTo("NONE ▼"));
        Assert.That(Editor.Handles, Has.Count.EqualTo(4));
    }

    [Test]
    public void ShapesAndPointsAreAddedAndRemovedThroughTheHost()
    {
        Click(XifEditorActionKind.AddPolygon);
        Assert.That(_host.Current!.Polygons, Has.Count.EqualTo(2));
        Assert.That(Editor.Polygon, Is.EqualTo(1));

        Click(XifEditorActionKind.InsertPoint);
        Assert.That(Polygon.Points, Has.Count.EqualTo(5));
        Assert.That(Polygon.Points[1], Is.EqualTo(new Vector2(0, -16)), "half way along the edge");
        Assert.That(Editor.Handles, Has.Count.EqualTo(5));

        Click(XifEditorActionKind.RemovePolygon);
        Click(XifEditorActionKind.RemovePolygon);
        Assert.That(_host.Current!.Polygons, Has.Count.EqualTo(1));
        Assert.That(Text(Editor.Parts.Status), Is.EqualTo("AN ITEM KEEPS ONE SHAPE"));
        Assert.That(_host.Edits, Is.EqualTo(3), "a refused edit records nothing");
    }

    [Test]
    public void AShapeKeepsThreePoints()
    {
        Click(XifEditorActionKind.RemovePoint);
        Click(XifEditorActionKind.RemovePoint);

        Assert.That(Polygon.Points, Has.Count.EqualTo(XifEditing.MinPolygonPoints));
        Assert.That(Text(Editor.Parts.Status), Is.EqualTo($"A SHAPE KEEPS {XifEditing.MinPolygonPoints} POINTS"));
    }

    [Test]
    public void AQuadKeepsItsShape()
    {
        Click(XifEditorActionKind.AddQuad);
        Click(XifEditorActionKind.InsertPoint);

        Assert.That(Polygon.Kind, Is.EqualTo(XifShapeKind.Quad));
        Assert.That(Text(Editor.Parts.Status), Does.EndWith("KEEPS ITS SHAPE · DRAG ITS HANDLES"));
    }

    [Test]
    public void TheNameAppliesWhenItsFieldLosesFocus()
    {
        Type(XifEditorFieldKind.Name, "  slash ");

        Assert.That(_host.Current!.Name, Is.EqualTo("slash"));
        Assert.That(Text(Editor.Parts.Status), Is.EqualTo("RENAMED"));
    }

    [Test]
    public void TheShaderIsPickedFromItsTab()
    {
        Entity button = UiTabbedPickers.ButtonOf(Editor.Parts.Shader);
        Assert.That(Editor.Parts.Shader.GetComponent<UiTabbedPicker>().Tabs.Select(tab => tab.Name),
            Is.EqualTo(new[] { "GAME", "LAB" }));

        XifEditorShaders.PickShader(_panel, 1, 0);
        Frame();
        Assert.That(_host.Current!.ShaderRef.Path, Is.EqualTo("glow.gdshader"));
        Assert.That(Text(button), Is.EqualTo("GLOW ▼"));

        XifEditorShaders.PickShader(_panel, 0, 0);
        Frame();
        Assert.That(_host.Current!.ShaderRef.IsNone, Is.True);

        XifEditing.SetShader(_host.Current!, XifShaderRef.OfMap(4));
        Frame();
        Assert.That(Text(button), Is.EqualTo("NOT ALLOWED HERE ▼"));
    }

    [Test]
    public void DraggingAPointMovesItAsOneGesture()
    {
        Vector2 start = PointOnScreen(2);
        _pointer.Current = new PointerInput(start, true);
        Frame();
        Assert.That(Editor.Dragging, Is.True);
        Assert.That(Editor.Point, Is.EqualTo(2));

        Transform2D toScreen = XifEditorView.ToScreen(Editor.View, XifEditorSystem.Area(Editor.Parts.Canvas));
        _pointer.Current = new PointerInput(toScreen * new Vector2(30.3f, 20.6f), true);
        Frame(3);
        _pointer.Current = new PointerInput(toScreen * new Vector2(30.3f, 20.6f), false);
        Frame();

        Assert.That(Polygon.Points[2], Is.EqualTo(new Vector2(30, 21)), "snapped to whole units");
        Assert.That(_host.Gestures, Is.EqualTo(1));
        Assert.That(_host.Open, Is.False);
        Assert.That(_host.Changes, Is.GreaterThan(0));
        Assert.That(_host.Edits, Is.EqualTo(0));
    }

    [Test]
    public void APressAwayFromEveryPointDoesNothing()
    {
        Rect2 box = XifEditorSystem.Area(Editor.Parts.Canvas);
        _pointer.Current = new PointerInput(box.Position + new Vector2(2, 2), true);
        Frame();

        Assert.That(Editor.Dragging, Is.False);
        Assert.That(_host.Gestures, Is.EqualTo(0));
    }

    [Test]
    public void TheEditingRulesHoldWithoutTheUi()
    {
        var item = new Xif();
        int index = XifEditing.AddPolygon(item);
        XifPolygon polygon = item.Polygons[index];

        Assert.That(XifEditing.Rename(item, "same"), Is.True);
        Assert.That(XifEditing.Rename(item, " same "), Is.False);
        Assert.That(XifEditing.MovePoint(polygon, 0, new Vector2(float.NaN, 0)), Is.False);
        Assert.That(XifEditing.TryParseFill("#00ff00", out Color green), Is.True);
        Assert.That(XifEditing.FormatFill(green), Is.EqualTo("#00ff00ff"));
        Assert.That(XifEditing.SetFill(polygon, green), Is.True);
        Assert.That(XifEditing.SetFill(polygon, green), Is.False, "no change, no edit");
        Assert.That(XifEditing.Bounds(item), Is.EqualTo(new Rect2(-16, -16, 32, 32)));
    }

    private sealed class FakePointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }

    /// <summary>An owner answering the editor's signals the way a tool would.</summary>
    private sealed class FakeHost
    {
        public Xif? Current = new()
        {
            Name = "streak",
            Modules = { new XifGeometry { Polygons = { new XifPolygon { Points = XifEditing.DefaultOutline() } } } },
        };

        public int Edits;
        public int Gestures;
        public int Changes;
        public bool Open;

        public static readonly IReadOnlyList<XifShaderChoice> Shaders =
        [
            new("NONE", new XifShaderRef(), "GAME"),
            new("GLOW", XifShaderRef.OfCommon("glow.gdshader"), "LAB"),
        ];

        public void Listen(Entity panel)
        {
            panel.AddSignalHandler<XifEditorFrameDue>(signal =>
            {
                ref XifEditorFrame frame = ref signal.Entity.GetComponent<XifEditorFrame>();
                frame.Item = Current;
                frame.Title = $"{Current?.Name} item";
            });
            panel.AddSignalHandler<XifEditAsked>(signal =>
            {
                Edits++;
                signal.Event.Change();
            });
            panel.AddSignalHandler<XifGestureStarted>(_ =>
            {
                Gestures++;
                Open = true;
            });
            panel.AddSignalHandler<XifGestureEnded>(_ => Open = false);
            panel.AddSignalHandler<XifChanged>(_ => Changes++);
        }
    }
}
