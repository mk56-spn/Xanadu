using System;
using Godot;

namespace Xanadu.Lab.Experiments.RenderSpike;

/// <summary>
/// Step 0 of docs/render-architecture.md: one panel per Godot assumption the
/// layering plan leans on, so a single shot answers them all.
/// </summary>
public sealed class RenderSpikeExperiment : ILabExperiment
{
    public string Name => "RENDER SPIKE";

    public string[] Notes =>
    [
        "Step 0 of docs/render-architecture.md.",
        "Each panel tests one assumption: CanvasGroup",
        "fades, z inside groups, screen reads, back-buffer",
        "copies, SubViewport layers, blend variants and",
        "2D instance uniforms.",
    ];

    public string? SourcePath => "res://Experiments/RenderSpike/screen_wave.gdshader";

    public Node2D CreateStage(Rect2 area) => new RenderSpikeStage(area);
}

/// <summary>Eight panels in a 4 × 2 grid.</summary>
public partial class RenderSpikeStage : Node2D
{
    private const float Gap = 16;
    private const float LabelHeight = 28;

    public RenderSpikeStage() { }

    public RenderSpikeStage(Rect2 area)
    {
        Action<Node2D, Vector2>[] panels =
        [
            UniformFade, ZInsideGroup, ScreenReadInGroup, CopyBetweenMembers,
            FreshScreenRead, ViewportLayer, BlendVariants, InstanceUniforms,
        ];
        string[] titles =
        [
            "A  group fade: plain | grouped",
            "B  z_index inside a group",
            "C  screen read in group (Q4)",
            "D  back-buffer copy in group",
            "E  copy: none loc VP full cnv px",
            "F  SubViewport layer",
            "G  blend: mix add mul",
            "H  instance uniforms",
        ];

        var cell = new Vector2((area.Size.X - Gap * 3) / 4, (area.Size.Y - Gap) / 2);
        for (int i = 0; i < panels.Length; i++)
        {
            var origin = area.Position + new Vector2((cell.X + Gap) * (i % 4), (cell.Y + Gap) * (i / 4));
            var panel = new Node2D { Position = origin + new Vector2(0, LabelHeight) };
            AddChild(new Label2D(titles[i]) { Position = origin + new Vector2(0, 20) });
            AddChild(panel);
            panels[i](panel, cell - new Vector2(0, LabelHeight));
        }
    }

    private static readonly Color Orange = new(1f, 0.55f, 0.1f);
    private static readonly Color Blue = new(0.2f, 0.4f, 1f);
    private static readonly Color Red = new(1f, 0.15f, 0.15f);

    private static ShaderMaterial Shaded(string name) =>
        new() { Shader = GD.Load<Shader>($"res://Experiments/RenderSpike/{name}.gdshader") };

    /// <summary>Plain translucent boxes darken where they overlap; a CanvasGroup with half alpha should not.</summary>
    private static void UniformFade(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        var half = new Color(1, 1, 1, 0.5f);
        for (int i = 0; i < 3; i++)
            panel.AddChild(new Box(new Vector2(90, 90), Orange * half) { Position = new Vector2(20 + i * 40, 40 + i * 40) });

        var group = new CanvasGroup { SelfModulate = half, Position = new Vector2(size.X / 2, 0) };
        for (int i = 0; i < 3; i++)
            group.AddChild(new Box(new Vector2(90, 90), Orange) { Position = new Vector2(20 + i * 40, 40 + i * 40) });
        panel.AddChild(group);
    }

    /// <summary>
    /// Red has z 1 but comes first; blue comes second at z 0. If z works inside the
    /// group, red is on top and both are half faded. If z escapes the group, red is
    /// drawn outside it at full strength.
    /// </summary>
    private static void ZInsideGroup(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        var group = new CanvasGroup { SelfModulate = new Color(1, 1, 1, 0.5f) };
        group.AddChild(new Box(new Vector2(150, 150), Red) { Position = new Vector2(40, 40), ZIndex = 1 });
        group.AddChild(new Box(new Vector2(150, 150), Blue) { Position = new Vector2(110, 110) });
        panel.AddChild(group);
        panel.AddChild(new Box(new Vector2(60, 60), Red) { Position = new Vector2(size.X - 80, 20) });
    }

    /// <summary>
    /// The green wave reads the screen from inside a group. Wavy stripes = it sees the
    /// screen from before the group (wanted). Orange and dark = it sees the group's buffer.
    /// </summary>
    private static void ScreenReadInGroup(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        var group = new CanvasGroup { SelfModulate = new Color(1, 1, 1, 0.85f) };
        group.AddChild(new Box(new Vector2(160, 120), Orange) { Position = new Vector2(30, 40) });
        group.AddChild(new Box(new Vector2(200, 200), Colors.White)
        {
            Position = new Vector2(110, 100),
            Material = Shaded("screen_wave"),
        });
        panel.AddChild(group);
    }

    /// <summary>As C, with a BackBufferCopy between the two members: does the orange box survive?</summary>
    private static void CopyBetweenMembers(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        var group = new CanvasGroup { SelfModulate = new Color(1, 1, 1, 0.85f) };
        group.AddChild(new Box(new Vector2(160, 120), Orange) { Position = new Vector2(30, 40) });
        // A full copy: rect copies take render-target pixels (panel E), so a full one is the honest test.
        group.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
        group.AddChild(new Box(new Vector2(200, 200), Colors.White)
        {
            Position = new Vector2(110, 100),
            Material = Shaded("screen_wave"),
        });
        panel.AddChild(group);
    }

    /// <summary>
    /// After an earlier screen read (the top band), six cyan boxes, each poking out
    /// below an inverting box. Fresh read = red over the cyan; stale = inverted stripes.
    /// Columns: no copy; rect copy in local space (stale); BackBufferCopy viewport
    /// mode; full copy flag on the item; rect in canvas space (stale); rect in
    /// render-target pixels (fresh). Rect copies take render-target pixels.
    /// </summary>
    private static void FreshScreenRead(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        ShaderMaterial invert = Shaded("screen_invert");
        panel.AddChild(new Box(new Vector2(size.X - 40, 60), Colors.White) { Position = new Vector2(20, 20), Material = invert });

        var cyan = new Color(0f, 1f, 1f);
        float column = (size.X - 20) / 6;
        for (int i = 0; i < 6; i++)
        {
            float x = 10 + i * column;
            panel.AddChild(new Box(new Vector2(column - 20, 120), cyan) { Position = new Vector2(x + 5, 150) });
            if (i == 2)
                panel.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
            var boxSize = new Vector2(column - 10, 100);
            Box box = i switch
            {
                1 => new CopyingBox(boxSize, Colors.White, CopyingBox.Space.Local),
                3 => new CopyingBox(boxSize, Colors.White, CopyingBox.Space.Full),
                4 => new CopyingBox(boxSize, Colors.White, CopyingBox.Space.Canvas),
                5 => new CopyingBox(boxSize, Colors.White, CopyingBox.Space.Pixels),
                _ => new Box(boxSize, Colors.White),
            };
            box.Position = new Vector2(x, 120);
            box.Material = invert;
            panel.AddChild(box);
        }
    }

    /// <summary>
    /// A layer drawn into its own SubViewport and composited with a pass shader.
    /// The boxes move and zoom fast; the red outlines are the same shapes drawn
    /// straight into the main canvas, so lag or a wrong transform shows as a gap.
    /// The left half-white square is in the viewport, the right one is drawn direct:
    /// equal brightness means the viewport's alpha composites correctly.
    /// </summary>
    private static void ViewportLayer(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        panel.AddChild(new ViewportLayer(size));
    }

    /// <summary>The same purple shader made as mix, add and mul variants by injecting render_mode.</summary>
    private static void BlendVariants(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        panel.AddChild(new Box(new Vector2(size.X, size.Y / 2), new Color(0.5f, 0.5f, 0.5f)));
        const string source = "shader_type canvas_item;\nvoid fragment() { COLOR = vec4(0.5, 0.15, 0.6, 1.0); }\n";
        string[] modes = ["blend_mix", "blend_add", "blend_mul"];
        float width = (size.X - 40) / modes.Length;
        for (int i = 0; i < modes.Length; i++)
        {
            var shader = new Shader { Code = source.Replace("shader_type canvas_item;", $"shader_type canvas_item;\nrender_mode {modes[i]};") };
            panel.AddChild(new Box(new Vector2(width - 10, size.Y - 80), Colors.White)
            {
                Position = new Vector2(20 + i * width, 40),
                Material = new ShaderMaterial { Shader = shader },
            });
        }
    }

    /// <summary>Three boxes share one ShaderMaterial; each sets its own instance uniform through the RenderingServer.</summary>
    private static void InstanceUniforms(Node2D panel, Vector2 size)
    {
        panel.AddChild(new Stripes(size));
        ShaderMaterial shared = Shaded("instance_tint");
        Color[] tints = [Red, Orange, Blue];
        float width = (size.X - 40) / tints.Length;
        for (int i = 0; i < tints.Length; i++)
            panel.AddChild(new InstanceBox(new Vector2(width - 10, size.Y - 80), tints[i])
            {
                Position = new Vector2(20 + i * width, 40),
                Material = shared,
            });
    }
}

/// <summary>Self-contained layer: content in a SubViewport, a composite quad in the main canvas.</summary>
public partial class ViewportLayer : Node2D
{
    private readonly Vector2 _size;
    private readonly SubViewport _viewport = new();
    private readonly Node2D _content = new();
    private readonly Node2D _reference = new();
    private readonly Sprite2D _composite = new();
    private float _time;

    public ViewportLayer() { }

    public ViewportLayer(Vector2 size)
    {
        _size = size;
        _viewport.TransparentBg = true;
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _viewport.Disable3D = true;
        AddChild(_viewport);
        _viewport.AddChild(_content);

        // The layer's content: overlapping translucent boxes, a buffered group, and a
        // screen read that should see only this layer.
        var half = new Color(1, 1, 1, 0.5f);
        _content.AddChild(new Box(new Vector2(70, 70), new Color(1f, 0.55f, 0.1f) * half) { Position = new Vector2(30, 30) });
        _content.AddChild(new Box(new Vector2(70, 70), new Color(1f, 0.55f, 0.1f) * half) { Position = new Vector2(60, 60) });
        var group = new CanvasGroup { SelfModulate = half };
        group.AddChild(new Box(new Vector2(70, 70), new Color(0.2f, 0.4f, 1f)) { Position = new Vector2(150, 30) });
        group.AddChild(new Box(new Vector2(70, 70), new Color(0.2f, 0.4f, 1f)) { Position = new Vector2(180, 60) });
        _content.AddChild(group);
        _content.AddChild(new Box(new Vector2(100, 60), Colors.White)
        {
            Position = new Vector2(120, 150),
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Experiments/RenderSpike/screen_wave.gdshader") },
        });
        _content.AddChild(new Box(new Vector2(60, 60), half) { Position = new Vector2(30, 160) });

        _composite.Texture = _viewport.GetTexture();
        _composite.Centered = false;
        _composite.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Experiments/RenderSpike/pass_split.gdshader") };
        AddChild(_composite);

        // The same shapes as outlines, straight into the main canvas.
        _reference.AddChild(new Box(new Vector2(70, 70), Colors.Red, outline: true) { Position = new Vector2(30, 30) });
        _reference.AddChild(new Box(new Vector2(70, 70), Colors.Red, outline: true) { Position = new Vector2(150, 30) });
        _reference.AddChild(new Box(new Vector2(100, 60), Colors.Red, outline: true) { Position = new Vector2(120, 150) });
        _reference.AddChild(new Box(new Vector2(60, 60), Colors.Red, outline: true) { Position = new Vector2(30, 160) });
        AddChild(_reference);
        AddChild(new Box(new Vector2(60, 60), half) { Position = new Vector2(_size.X - 90, _size.Y - 90) });
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        // Stand-in for the editor's zoom and pan: fast, so a frame of lag is visible.
        var camera = new Transform2D(0, Vector2.One * (1f + 0.15f * Mathf.Sin(_time * 3f)), 0,
            new Vector2(30f + 40f * Mathf.Sin(_time * 10f), 20f));
        _reference.Transform = camera;

        // The viewport renders at the real screen pixel size of the panel, and the
        // composite scales back down, so the layer stays sharp under stretch.
        float scale = GetScreenTransform().Scale.X;
        var pixels = new Vector2I(Mathf.CeilToInt(_size.X * scale), Mathf.CeilToInt(_size.Y * scale));
        if (_viewport.Size != pixels)
            _viewport.Size = pixels;
        _viewport.CanvasTransform = Transform2D.Identity.Scaled(Vector2.One * scale);
        _content.Transform = camera;
        _composite.Scale = Vector2.One / scale;
    }
}

/// <summary>A filled or outlined rect.</summary>
public partial class Box : Node2D
{
    private readonly Vector2 _size;
    private readonly Color _color;
    private readonly bool _outline;

    public Box() { }

    public Box(Vector2 size, Color color, bool outline = false)
    {
        _size = size;
        _color = color;
        _outline = outline;
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, _size), _color, !_outline, _outline ? 2 : -1);
}

/// <summary>A box whose canvas item copies the screen to the back buffer before it draws.</summary>
public partial class CopyingBox : Box
{
    /// <summary>Which space the copy rect is given in.</summary>
    public enum Space { Full, Local, Canvas, Pixels }

    private readonly Vector2 _boxSize;
    private readonly Space _space;

    public CopyingBox() { }

    public CopyingBox(Vector2 size, Color color, Space space) : base(size, color)
    {
        _boxSize = size;
        _space = space;
    }

    public override void _Process(double delta)
    {
        // A little larger than the box. The stage doesn't move, but a real
        // screen-read region would recompute this every frame, as here.
        var local = new Rect2(new Vector2(-5, -5), _boxSize + new Vector2(10, 10));
        Rect2 canvas = GetGlobalTransformWithCanvas() * local;
        Rect2 rect = _space switch
        {
            Space.Full => new Rect2(),
            Space.Local => local,
            Space.Canvas => canvas,
            _ => GetViewport().GetFinalTransform() * canvas,
        };
        RenderingServer.CanvasItemSetCopyToBackbuffer(GetCanvasItem(), true, rect);
    }
}

/// <summary>A box that sets its own instance uniform once it has a canvas item.</summary>
public partial class InstanceBox : Box
{
    private readonly Color _tint;

    public InstanceBox() { }

    public InstanceBox(Vector2 size, Color tint) : base(size, Colors.White) => _tint = tint;

    public override void _Ready() =>
        RenderingServer.CanvasItemSetInstanceShaderParameter(GetCanvasItem(), "tint", _tint);
}

/// <summary>Black and light vertical stripes, so screen reads and fades are easy to judge.</summary>
public partial class Stripes : Node2D
{
    private readonly Vector2 _size;

    public Stripes() { }

    public Stripes(Vector2 size) => _size = size;

    public override void _Draw()
    {
        const float width = 12;
        for (int i = 0; i * width < _size.X; i++)
        {
            Color color = i % 2 == 0 ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.85f, 0.85f, 0.8f);
            DrawRect(new Rect2(i * width, 0, Math.Min(width, _size.X - i * width), _size.Y), color);
        }
    }
}

/// <summary>A caption in the fallback font.</summary>
public partial class Label2D : Node2D
{
    private readonly string _text = "";

    public Label2D() { }

    public Label2D(string text) => _text = text;

    public override void _Draw() => DrawString(ThemeDB.FallbackFont, Vector2.Zero, _text, fontSize: 18, modulate: Colors.White);
}
