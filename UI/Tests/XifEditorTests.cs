using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Systems.Input;
using Xanadu.Systems.Layout;
using Xanadu.Systems.Ui;
using Xanadu.Tests.Support;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Tests.UI;

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
    private XifPrimitive Polygon => _host.Current!.Primitives[Editor.Primitive];

    protected override void OnEcsSetUp()
    {
        _pointer = new FakePointer();
        _host = new FakeHost();
        Root.Add(new ContainerLayoutSystem());
        Root.Add(new XifEditorSystem(_pointer, new FakeVisualRidFactory()));
        UiNode layer = UiNode.Layer(Store, default, 5, new FakeVisualRidFactory());
        _panel = XifEditorPanel.Build(layer, Home, _host);
        Frame(2);
    }

    private Entity ButtonOf(XifEditorActionKind kind)
    {
        foreach (Entity entity in Store.Query<XifEditorAction>().ToEntityList())
        {
            if (entity.GetComponent<XifEditorAction>().Kind == kind)
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

        Assert.That(_panel.GetComponent<Position>().Value, Is.EqualTo(UiLayout.Hidden));
    }

    [Test]
    public void ItsReadoutsDescribeTheItem()
    {
        Assert.That(Text(Editor.Parts.Title), Is.EqualTo("STREAK ITEM"));
        Assert.That(Text(Editor.Parts.PrimitiveLabel), Is.EqualTo("POLYGON 1/1"));
        Assert.That(Text(Editor.Parts.PointLabel), Is.EqualTo("PT 1/4  -16, -16"));
        Assert.That(Text(Editor.Parts.Shader), Is.EqualTo("NONE"));
        Assert.That(Editor.Handles, Has.Count.EqualTo(4));
    }

    [Test]
    public void PolygonsAndPointsAreAddedAndRemovedThroughTheHost()
    {
        Click(XifEditorActionKind.AddPolygon);
        Assert.That(_host.Current!.Primitives, Has.Count.EqualTo(2));
        Assert.That(Text(Editor.Parts.PrimitiveLabel), Is.EqualTo("POLYGON 2/2"));

        Click(XifEditorActionKind.InsertPoint);
        Assert.That(Polygon.Points, Has.Count.EqualTo(5));
        Assert.That(Polygon.Points[1], Is.EqualTo(new Vector2(0, -16)), "half way along the edge");
        Assert.That(Editor.Handles, Has.Count.EqualTo(5));

        Click(XifEditorActionKind.RemovePrimitive);
        Click(XifEditorActionKind.RemovePrimitive);
        Assert.That(_host.Current!.Primitives, Has.Count.EqualTo(1));
        Assert.That(Text(Editor.Parts.Status), Is.EqualTo("AN ITEM KEEPS ONE PRIMITIVE"));
        Assert.That(_host.Edits, Is.EqualTo(3), "a refused edit records nothing");
    }

    [Test]
    public void APolygonKeepsThreePoints()
    {
        Click(XifEditorActionKind.RemovePoint);
        Click(XifEditorActionKind.RemovePoint);

        Assert.That(Polygon.Points, Has.Count.EqualTo(XifEditing.MinPolygonPoints));
        Assert.That(Text(Editor.Parts.Status), Does.StartWith("A POLYGON KEEPS 3"));
    }

    [Test]
    public void TypedFieldsApplyWhenTheyLoseFocus()
    {
        Type(XifEditorFieldKind.Name, "  slash ");
        Type(XifEditorFieldKind.Fill, "#ff800080");
        Type(XifEditorFieldKind.Texture, "res://streak.png");

        Assert.That(_host.Current!.Name, Is.EqualTo("slash"));
        Assert.That(Polygon.Fill, Is.EqualTo(Color.FromHtml("#ff800080")));
        Assert.That(Polygon.Ref, Is.EqualTo("res://streak.png"));
        Assert.That(Text(Editor.Parts.Status), Is.EqualTo("TEXTURE SET"));

        Type(XifEditorFieldKind.Fill, "orange-ish");
        Assert.That(Polygon.Fill, Is.EqualTo(Color.FromHtml("#ff800080")));
        Assert.That(Text(Editor.Parts.Status), Is.EqualTo("FILL IS #RRGGBB OR #RRGGBBAA"));
    }

    [Test]
    public void TheShaderCyclesThroughWhatTheHostAllows()
    {
        Click(XifEditorActionKind.NextShader);
        Assert.That(Polygon.Shader.Path, Is.EqualTo("res://glow.gdshader"));
        Assert.That(Text(Editor.Parts.Shader), Is.EqualTo("GLOW"));

        Click(XifEditorActionKind.NextShader);
        Assert.That(Polygon.Shader.IsNone, Is.True);

        Polygon.Shader = XifShaderRef.OfMap(4);
        Frame();
        Assert.That(Text(Editor.Parts.Shader), Is.EqualTo("NOT ALLOWED HERE"));
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
        XifPrimitive polygon = item.Primitives[index];

        Assert.That(XifEditing.Rename(item, "same"), Is.True);
        Assert.That(XifEditing.Rename(item, " same "), Is.False);
        Assert.That(XifEditing.MovePoint(polygon, 0, new Vector2(float.NaN, 0)), Is.False);
        Assert.That(XifEditing.TryParseFill("#00ff00", out Color green), Is.True);
        Assert.That(XifEditing.FormatFill(green), Is.EqualTo("#00ff00ff"));
        Assert.That(XifEditing.Bounds(item), Is.EqualTo(new Rect2(-16, -16, 32, 32)));
    }

    private sealed class FakePointer : IPointerInputSource
    {
        public PointerInput Current;

        public PointerInput Read() => Current;
    }

    private sealed class FakeHost : IXifEditorHost
    {
        public Xif? Current = new()
        {
            Name = "streak",
            Primitives = { new XifPrimitive { Kind = XifPrimitive.PolygonKind, Points = XifEditing.DefaultOutline() } },
        };

        public int Edits;
        public int Gestures;
        public int Changes;
        public bool Open;

        public Xif? Item => Current;
        public string Title => $"{Current?.Name} item";

        public IReadOnlyList<XifShaderChoice> Shaders { get; } = new List<XifShaderChoice>
        {
            new("NONE", new XifShaderRef()),
            new("GLOW", XifShaderRef.OfCommon("res://glow.gdshader")),
        };

        public bool CanLoad(string path) => path.EndsWith(".png", StringComparison.Ordinal);

        public void Edit(Action change)
        {
            Edits++;
            change();
        }

        public void BeginGesture()
        {
            Gestures++;
            Open = true;
        }

        public void EndGesture() => Open = false;

        public void Changed() => Changes++;
    }
}
