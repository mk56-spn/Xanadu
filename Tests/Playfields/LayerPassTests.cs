using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using NUnit.Framework;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Shaders;
using Xanadu.Tests.Support;

namespace Xanadu.Tests.Playfields;

/// <summary>
/// Step 9 of docs/render-architecture.md: a layer's pass either composites its
/// own viewport (self-contained) or reads the screen behind it (background reader).
/// </summary>
[TestFixture]
public sealed class LayerPassTests
{
    private static readonly PlayfieldView View = new(Vector2.Zero, 1f, new Vector2(800, 600));
    private static readonly Playfield Field = new(1000, 1000);
    private static readonly Playhead Head = new(0, 120, 0, false);
    private static readonly Rid Main = TestRid.Make(1);

    private static EntityStore Level(LevelLayers layers)
    {
        var store = new EntityStore();
        store.CreateEntity(
            new Note(0, new Vector2(300, 300)),
            new ItemLayer(-2, 0));
        store.CreateEntity(layers);
        return store;
    }

    private static LevelLayers SelfContained(int slot = 3, float tint = 0.5f)
    {
        LevelLayers table = LevelLayers.Create();
        LevelLayer layer = table.Edit(-2);
        layer.Pass = LayerPassKind.SelfContained;
        layer.PassShader = slot;
        layer.PassKeys.Add(new ShaderKey("tint", 0f, new Vector4(tint, 0f, 0f, 1f), 4, GroupEase.Linear));
        return table;
    }

    private static void Draw(
        IRectCanvas canvas,
        EntityStore store,
        LevelLayers table,
        IPassSurfaces? passes,
        FakeItems items,
        GroupDefinition[]? groups = null) =>
        PlayfieldRenderer.DrawLayers(
            new ShaderRoutingCanvas(canvas, Main, items),
            Main,
            store,
            new PlayfieldItemFrame(
                View,
                Field,
                Head,
                0f,
                Editor: false,
                Layers: table.Layers,
                Passes: passes,
                Groups: groups),
            drawWedge: null);

    [Test]
    public void ASelfContainedLayerDrawsIntoItsOwnCanvasAndComposites()
    {
        LevelLayers table = SelfContained();
        EntityStore store = Level(table);
        var canvas = new PassCanvas();
        var surfaces = new FakeSurfaces();
        var items = new FakeItems();

        Draw(canvas, store, table, surfaces, items);

        Assert.That(items.Parents.Select(pair => pair.Parent), Does.Contain(surfaces.Fake.Canvas), "the item runs are parented into it");
        Assert.That(items.Materials, Does.Contain((3, BlendMode.Premultiplied)));
        Assert.That(items.Values, Does.Contain(("tint", new Vector4(0.5f, 0f, 0f, 1f), 4)));
        Assert.That(surfaces.Fake.TextureReads, Is.EqualTo(1), "the viewport is composited into the main canvas");
    }

    [Test]
    public void ABackgroundReaderCopiesTheScreenBeforeItsQuad()
    {
        LevelLayers table = LevelLayers.Create();
        LevelLayer layer = table.Edit(-2);
        layer.Pass = LayerPassKind.BackgroundReader;
        layer.PassShader = 2;
        layer.PassKeys.Add(new ShaderKey("strength", 0f, new Vector4(0.25f, 0, 0, 0), 1, GroupEase.Linear));
        EntityStore store = Level(table);
        var canvas = new PassCanvas();
        var items = new FakeItems();

        Draw(canvas, store, table, new FakeSurfaces(), items);

        Assert.That(items.Copies, Has.Count.EqualTo(1), "the quad's run copies the screen first");
        Assert.That(items.Materials, Does.Contain((2, BlendMode.Mix)));
        Assert.That(items.Values, Does.Contain(("strength", new Vector4(0.25f, 0, 0, 0), 1)));
    }

    [Test]
    public void APlainLayerHasNoPassWork()
    {
        LevelLayers table = LevelLayers.Create();
        EntityStore store = Level(table);
        var canvas = new PassCanvas();
        var surfaces = new FakeSurfaces();
        var items = new FakeItems();

        Draw(canvas, store, table, surfaces, items);

        Assert.That(surfaces.Created, Is.Zero);
        Assert.That(items.Copies, Is.Empty);
        Assert.That(canvas.Composites, Is.Zero);
    }

    private sealed class FakeSurfaces : IPassSurfaces
    {
        public FakeSurface Fake { get; } = new();
        public int Created { get; private set; }

        public IPassSurface For(int layer, Vector2 size)
        {
            Created++;
            return Fake;
        }

        public void Release(int layer) { }

        public void ReleaseAll() { }

        public sealed class FakeSurface : IPassSurface
        {
            public Rid Canvas { get; } = TestRid.Make(77);
            public int TextureReads { get; private set; }

            public Texture2D? Texture
            {
                get
                {
                    TextureReads++;
                    return null;
                }
            }

            public void Resize(Vector2 size) { }
        }
    }

    [Test]
    public void ABufferedGroupDrawsItsMembersIntoOneGroupedImage()
    {
        LevelLayers table = LevelLayers.Create();
        var store = new EntityStore();
        Entity a = store.CreateEntity(new Note(0, new Vector2(100, 100)), new ItemLayer(-2, 0), new ItemGroups(ItemGroups.Bit(0)));
        Entity b = store.CreateEntity(new Note(0, new Vector2(200, 200)), new ItemLayer(-2, 1), new ItemGroups(ItemGroups.Bit(0)));
        store.CreateEntity(table);
        GroupDefinition[] groups = new GroupDefinition[GroupLimits.Count];
        for (int i = 0; i < groups.Length; i++)
            groups[i] = new GroupDefinition();
        groups[0].Buffered = true;
        groups[0].BufferShader = 2;
        groups[0].BufferBlend = BlendMode.Add;
        groups[0].BufferTint = new Color("#3366cc80");
        groups[0].BufferAlpha = 0.5f;
        groups[0].BufferKeys.Add(new ShaderKey("size", 0f, new Vector4(4f, 0, 0, 0), 1, GroupEase.Linear));
        var canvas = new PassCanvas();
        var items = new FakeItems();

        Draw(canvas, store, table, null, items, groups);

        Assert.That(items.Grouped, Has.Count.EqualTo(1), "one grouped canvas item");
        Assert.That(items.Modulates.Single().Color.R, Is.EqualTo(0.2f).Within(0.01f));
        Assert.That(items.Modulates.Single().Color.A, Is.EqualTo(0.5f * 0.5f).Within(0.01f));
        Assert.That(items.Materials, Does.Contain((2, BlendMode.Add)));
        Assert.That(items.Values, Does.Contain(("size", new Vector4(4f, 0, 0, 0), 1)));
    }

    private sealed class FakeItems : IShaderCanvasItems
    {
        private int _next = 100;

        public List<(Rid Item, Rid Parent)> Parents { get; } = new();
        public List<(int Slot, BlendMode Blend)> Materials { get; } = new();
        public List<(string Param, Vector4 Value, int Size)> Values { get; } = new();
        public List<Rid> Copies { get; } = new();

        public Rid CreateItem(Rid parent)
        {
            Rid item = TestRid.Make((ulong)_next++);
            Parents.Add((item, parent));
            return item;
        }

        public void SetMaterial(Rid item, int slot, string commonPath, BlendMode blend) =>
            Materials.Add((slot, blend));

        public void SetInstanceUniform(Rid item, string param, Vector4 value, int size) =>
            Values.Add((param, value, size));

        public void CopyScreen(Rid item) => Copies.Add(item);

        public List<Rid> Grouped { get; } = new();
        public List<(Rid Item, Color Color)> Modulates { get; } = new();

        public void SetCanvasGroup(Rid item) => Grouped.Add(item);

        public void SetModulate(Rid item, Color color) => Modulates.Add((item, color));
    }

    private sealed class PassCanvas : IRectCanvas
    {
        public List<(int Layer, Rid? Target)> LayerTargets { get; } = new();
        public List<Rid> DrawCanvases { get; } = new();
        public List<(int Slot, BlendMode Blend)> Selections { get; } = new();
        public List<(string Param, Vector4 Value, int Size)> Values { get; } = new();
        public List<string> Calls { get; } = new();
        public int Composites { get; private set; }

        public void BeginLayer(int layer, Rid? target = null) => LayerTargets.Add((layer, target));

        public void CopyScreen() => Calls.Add("copy");


        public void SelectShader(int slot, BlendMode blend = BlendMode.Mix, int sourceGroup = -1) =>
            Selections.Add((slot, blend));

        public void SetShaderValue(string param, Vector4 value, int size) => Values.Add((param, value, size));

        public void SetVisible(Rid canvas, bool visible) { }

        public void SetTransform(Rid canvas, Transform2D transform) { }

        public void Clear(Rid canvas) { }

        public void DrawRect(Rid canvas, Rect2 bounds, Color color) => Mark(canvas, "rect");

        public void DrawTexture(Rid canvas, Rect2 bounds, Texture2D texture)
        {
            Composites++;
            Mark(canvas, "texture");
        }

        public void DrawCircle(Rid canvas, Vector2 center, float radius, Color color) => Mark(canvas, "circle");

        public void DrawPolygon(Rid canvas, System.ReadOnlySpan<Vector2> points, Color color) => Mark(canvas, "polygon");

        public void DrawPolyline(
            Rid canvas,
            System.ReadOnlySpan<Vector2> points,
            Color color,
            float width,
            bool antialiased = true) => Mark(canvas, "polyline");

        public void DrawLine(Rid canvas, Vector2 from, Vector2 to, Color color, float width) => Mark(canvas, "line");

        public void DrawText(Rid canvas, Rect2 bounds, string text, Color color) => Mark(canvas, "text");

        public void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) => Mark(canvas, "text-left");

        public void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) => Mark(canvas, "text-right");

        private void Mark(Rid canvas, string call)
        {
            DrawCanvases.Add(canvas);
            Calls.Add(call);
        }
    }
}
