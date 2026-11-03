using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Lab.Experiments.Snow;

/// <summary>
/// A low-res 3D snow scene without Godot 3D: triangles built on the CPU, projected
/// by a canvas vertex shader, sorted far to near each frame, drawn into a small
/// viewport and blown up with nearest filtering.
/// </summary>
public sealed class SnowExperiment : ILabExperiment
{
    public const string Folder = "res://Experiments/Snow/";

    public string Name => "SNOW · 3D";

    public string[] Notes =>
    [
        "World xyz rides in VERTEX.xy + UV.x; vertex()",
        "projects it through a pinhole camera.",
        "No depth buffer: C# sorts the index list far",
        "to near each frame and culls back faces.",
        "Light is baked into COLOR; fog and dither on GPU.",
        "Flakes wrap around the camera, GPU only.",
        "Mouse turns the view; drawn at 1/4 resolution.",
    ];

    public string? SourcePath => Folder + "snow_scene.gdshader";

    public Node2D CreateStage(Rect2 area) => new SnowStage(area);
}

/// <summary>Owns the low-res viewport, the camera and the per-frame sort.</summary>
public partial class SnowStage : Node2D
{
    private const int PixelScale = 4;
    private const float OrbitRadius = 16f;
    private const float Near = 0.25f;

    private static readonly StringName CamPos = "cam_pos", CamRight = "cam_right", CamUp = "cam_up",
        CamFwd = "cam_fwd", ScreenCenter = "screen_center", Focal = "focal", SunDir = "sun_dir";

    private readonly Rect2 _area;
    private readonly SubViewport _viewport = null!;
    private readonly ShaderMaterial[] _materials = [];
    private readonly SnowMesh _mesh = null!;
    private readonly SnowLayer _scene = null!;
    private readonly Vector2 _size;
    private float[] _keys = [];
    private int[] _order = [];
    private float _time;

    public SnowStage() { }

    public SnowStage(Rect2 area)
    {
        _area = area;
        _size = (area.Size / PixelScale).Floor();
        _viewport = new SubViewport
        {
            Size = (Vector2I)_size,
            Disable3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        AddChild(_viewport);

        var sky = LoadMaterial("snow_sky");
        var scene = LoadMaterial("snow_scene");
        var flakes = LoadMaterial("snow_flakes");
        _materials = [sky, scene, flakes];

        _mesh = SnowWorld.Build();
        _keys = new float[_mesh.Triangles];
        _order = new int[_mesh.Triangles];

        _viewport.AddChild(new SnowSky(_size) { Material = sky });
        _scene = new SnowLayer { Material = scene };
        _viewport.AddChild(_scene);
        _viewport.AddChild(SnowFlakes.Build(flakes));

        AddChild(new Sprite2D
        {
            Texture = _viewport.GetTexture(),
            Centered = false,
            TextureFilter = TextureFilterEnum.Nearest,
            Scale = Vector2.One * PixelScale,
            Position = area.Position + (area.Size - _size * PixelScale) / 2,
        });
    }

    /// <summary>The shaders move every vertex, so the points' own bounds mean nothing for culling.</summary>
    public static void NeverCull(Node2D item) =>
        RenderingServer.CanvasItemSetCustomRect(item.GetCanvasItem(), true, new Rect2(-1e5f, -1e5f, 2e5f, 2e5f));

    private static ShaderMaterial LoadMaterial(string name) =>
        new() { Shader = GD.Load<Shader>(SnowExperiment.Folder + name + ".gdshader") };

    public override void _Process(double delta)
    {
        bool shown = IsVisibleInTree();
        _viewport.RenderTargetUpdateMode = shown ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (!shown)
            return;

        _time += (float)delta;
        (Vector3 eye, Basis view) = Camera();
        float focal = _size.X / 2 / Mathf.Tan(Mathf.DegToRad(36));
        foreach (ShaderMaterial m in _materials)
        {
            m.SetShaderParameter(CamPos, eye);
            m.SetShaderParameter(CamRight, view.X);
            m.SetShaderParameter(CamUp, view.Y);
            m.SetShaderParameter(CamFwd, view.Z);
            m.SetShaderParameter(ScreenCenter, _size / 2);
            m.SetShaderParameter(Focal, focal);
            m.SetShaderParameter(SunDir, SnowWorld.Sun);
        }
        _scene.Indices = Sort(eye, view.Z);
        _scene.Mesh = _mesh;
        _scene.QueueRedraw();
    }

    /// <summary>A slow orbit round the pond; the mouse turns the head on top of it.</summary>
    private (Vector3 Eye, Basis View) Camera()
    {
        float angle = _time * 0.05f + 0.6f;
        var eye = new Vector3(Mathf.Sin(angle) * OrbitRadius, 0, Mathf.Cos(angle) * OrbitRadius);
        eye.Y = SnowWorld.Height(eye.X, eye.Z) + 4.6f;

        Vector2 look = Vector2.Zero;
        if (_area.HasPoint(LabHost.Mouse))
            look = (LabHost.Mouse - _area.GetCenter()) / _area.Size;

        Vector3 toCentre = new Vector3(-eye.X, 0, -eye.Z).Normalized();
        Vector3 forward = toCentre.Rotated(Vector3.Up, -look.X * 2.4f);
        float pitch = -0.27f - look.Y * 0.7f;
        forward = (forward * Mathf.Cos(pitch) + Vector3.Up * Mathf.Sin(pitch)).Normalized();
        Vector3 right = forward.Cross(Vector3.Up).Normalized();
        Vector3 up = right.Cross(forward);
        return (eye, new Basis(right, up, forward));
    }

    /// <summary>Drops faces turned away or crossing the near plane, then orders the rest far to near.</summary>
    private int[] Sort(Vector3 eye, Vector3 forward)
    {
        int count = 0;
        for (int t = 0; t < _mesh.Triangles; t++)
        {
            Vector3 centre = _mesh.Centres[t];
            if (_mesh.Normals[t].Dot(eye - centre) <= 0)
                continue;
            int v = t * 3;
            if ((_mesh.Corners[v] - eye).Dot(forward) < Near
                || (_mesh.Corners[v + 1] - eye).Dot(forward) < Near
                || (_mesh.Corners[v + 2] - eye).Dot(forward) < Near)
                continue;
            _keys[count] = -((centre - eye).Dot(forward) + _mesh.Bias[t]);
            _order[count] = t;
            count++;
        }
        Array.Sort(_keys, _order, 0, count);

        var indices = new int[count * 3];
        for (int i = 0; i < count; i++)
        {
            int v = _order[i] * 3;
            indices[i * 3] = v;
            indices[i * 3 + 1] = v + 1;
            indices[i * 3 + 2] = v + 2;
        }
        return indices;
    }

    /// <summary>The terrain, trees, cabin and snowman: whatever indices the stage sorted last.</summary>
    public partial class SnowLayer : Node2D
    {
        public SnowMesh? Mesh;
        public int[] Indices = [];

        public override void _Ready() => NeverCull(this);

        public override void _Draw()
        {
            if (Mesh is null || Indices.Length == 0)
                return;
            RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), Indices, Mesh.Points, Mesh.Colors, Mesh.Uvs);
        }
    }

    /// <summary>A full-viewport rect for the sky shader.</summary>
    public partial class SnowSky : Node2D
    {
        private readonly Vector2 _size;

        public SnowSky() { }

        public SnowSky(Vector2 size) => _size = size;

        public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, _size), Colors.White);
    }
}

/// <summary>Flat-shaded triangles with unshared corners, packed the way the scene shader reads them.</summary>
public sealed class SnowMesh
{
    public int Triangles;
    public Vector3[] Corners = [];
    public Vector3[] Centres = [];
    public Vector3[] Normals = [];
    public float[] Bias = [];
    public Vector2[] Points = [];
    public Vector2[] Uvs = [];
    public Color[] Colors = [];
}

/// <summary>Collects triangles, lights each one once and packs the arrays.</summary>
public sealed class SnowBuilder
{
    private static readonly Color Ambient = new(0.42f, 0.50f, 0.72f);
    private static readonly Color SunLight = new(1.0f, 0.86f, 0.70f);

    private readonly List<Vector3> _corners = [];
    private readonly List<Vector3> _normals = [];
    private readonly List<Color> _colors = [];
    private readonly List<float> _bias = [];
    private readonly Random _random = new(7);

    public float Bias;

    /// <summary>Adds a lit triangle; <paramref name="outward"/> picks which side is its front.</summary>
    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color albedo, Vector3 outward, bool emissive = false)
    {
        Vector3 n = (b - a).Cross(c - a).Normalized();
        if (n.Dot(outward) < 0)
            n = -n;
        _corners.Add(a);
        _corners.Add(b);
        _corners.Add(c);
        _normals.Add(n);
        _bias.Add(Bias);
        _colors.Add(emissive ? albedo : Shade(albedo, n));
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color albedo, Vector3 outward, bool emissive = false)
    {
        Tri(a, b, c, albedo, outward, emissive);
        Tri(a, c, d, albedo, outward, emissive);
    }

    /// <summary>Warm low sun against a cold blue sky fill, plus a little per-face grain.</summary>
    private Color Shade(Color albedo, Vector3 n)
    {
        float sun = Mathf.Max(n.Dot(SnowWorld.Sun), 0);
        float sky = 0.55f + 0.45f * n.Y;
        float grain = 0.96f + (float)_random.NextDouble() * 0.06f;
        Color light = Ambient * sky + SunLight * sun * 0.95f;
        return new Color(albedo.R * light.R * grain, albedo.G * light.G * grain, albedo.B * light.B * grain);
    }

    public SnowMesh Pack()
    {
        int triangles = _normals.Count;
        var mesh = new SnowMesh
        {
            Triangles = triangles,
            Corners = _corners.ToArray(),
            Normals = _normals.ToArray(),
            Bias = _bias.ToArray(),
            Centres = new Vector3[triangles],
            Points = new Vector2[triangles * 3],
            Uvs = new Vector2[triangles * 3],
            Colors = new Color[triangles * 3],
        };
        for (int t = 0; t < triangles; t++)
        {
            int v = t * 3;
            mesh.Centres[t] = (_corners[v] + _corners[v + 1] + _corners[v + 2]) / 3;
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = _corners[v + k];
                mesh.Points[v + k] = new Vector2(p.X, p.Z);
                mesh.Uvs[v + k] = new Vector2(p.Y, 0);
                mesh.Colors[v + k] = _colors[t];
            }
        }
        return mesh;
    }
}

/// <summary>The scene itself: rolling snow, a frozen pond, pines, a cabin, a snowman and far peaks.</summary>
public static class SnowWorld
{
    public static readonly Vector3 Sun = new Vector3(-0.8f, 0.24f, -0.55f).Normalized();

    private const int Half = 32;
    private const float OrbitClearing = 16f;   // keep in step with SnowStage.OrbitRadius
    private const float Water = -0.45f;
    private static readonly Vector2 Pond = new(3.5f, -2.5f);
    private static readonly Vector2 Cabin = new(-6.5f, 5.5f);

    private static readonly Color Snow = new(0.97f, 0.98f, 1.0f);
    private static readonly Color Ice = new(0.55f, 0.72f, 0.86f);
    private static readonly Color Needles = new(0.13f, 0.30f, 0.24f);
    private static readonly Color Bark = new(0.36f, 0.24f, 0.17f);
    private static readonly Color Wood = new(0.52f, 0.33f, 0.21f);
    private static readonly Color Stone = new(0.48f, 0.47f, 0.50f);
    private static readonly Color Lamp = new(1.0f, 0.78f, 0.38f);
    private static readonly Color Carrot = new(0.95f, 0.45f, 0.12f);
    private static readonly Color Coal = new(0.10f, 0.10f, 0.12f);
    private static readonly Color Rock = new(0.55f, 0.60f, 0.70f);

    public static float Height(float x, float z)
    {
        float h = 0.6f * Mathf.Sin(x * 0.21f + 1.3f) * Mathf.Cos(z * 0.17f)
                  + 0.35f * Mathf.Sin(x * 0.47f + z * 0.31f)
                  + 0.2f * Mathf.Cos(z * 0.63f - x * 0.2f);
        float pond = new Vector2(x, z).DistanceTo(Pond);
        h -= 1.4f * Smooth(6.5f, 2.5f, pond);
        h = Mathf.Lerp(h, 0.1f, Smooth(3.5f, 1.5f, new Vector2(x, z).DistanceTo(Cabin)));
        float r = Mathf.Sqrt(x * x + z * z);
        float ridge = Smooth(19, 31, r);
        h += ridge * ridge * 10f * (0.75f + 0.25f * Mathf.Sin(Mathf.Atan2(z, x) * 5 + 1));
        return Mathf.Max(h, Water);
    }

    private static float Smooth(float from, float to, float x)
    {
        float t = Mathf.Clamp((x - from) / (to - from), 0, 1);
        return t * t * (3 - 2 * t);
    }

    public static SnowMesh Build()
    {
        var b = new SnowBuilder();
        var random = new Random(3);
        Mountains(b, random);
        Terrain(b);
        CabinAt(b, Cabin, 0.45f);
        Snowman(b, new Vector2(-0.5f, 3.0f));
        Trees(b, random);
        return b.Pack();
    }

    private static void Terrain(SnowBuilder b)
    {
        b.Bias = GroundBias;
        for (int z = -Half; z < Half; z++)
        for (int x = -Half; x < Half; x++)
        {
            Vector3 p00 = Corner(x, z), p10 = Corner(x + 1, z), p01 = Corner(x, z + 1), p11 = Corner(x + 1, z + 1);
            // Alternate the diagonal so the facets do not all lean one way.
            if (((x + z) & 1) == 0)
            {
                Ground(b, p00, p10, p11);
                Ground(b, p00, p11, p01);
            }
            else
            {
                Ground(b, p00, p10, p01);
                Ground(b, p10, p11, p01);
            }
        }
        b.Bias = 0;
    }

    /// <summary>Terrain sorts this much farther than it is, so things standing on it win ties.</summary>
    private const float GroundBias = 1.2f;

    private static Vector3 Corner(int x, int z) => new(x, Height(x, z), z);

    private static void Ground(SnowBuilder b, Vector3 a, Vector3 c, Vector3 d)
    {
        bool frozen = a.Y <= Water + 0.001f && c.Y <= Water + 0.001f && d.Y <= Water + 0.001f;
        b.Tri(a, c, d, frozen ? Ice : Snow, Vector3.Up);
    }

    /// <summary>A ring of far peaks, two facets each, so the fog has something to swallow.</summary>
    private static void Mountains(SnowBuilder b, Random random)
    {
        const int Peaks = 30;
        for (int i = 0; i < Peaks; i++)
        {
            float angle = i * Mathf.Tau / Peaks + (float)random.NextDouble() * 0.1f;
            float width = 0.22f + (float)random.NextDouble() * 0.12f;
            float radius = 95 + (float)random.NextDouble() * 25;
            float height = 18 + (float)random.NextDouble() * 22;
            Vector3 Ring(float a, float r, float y) => new(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            Vector3 left = Ring(angle - width, radius, -2), right = Ring(angle + width, radius, -2);
            Vector3 front = Ring(angle + ((float)random.NextDouble() - 0.5f) * width, radius - 14, -2);
            Vector3 peak = Ring(angle + ((float)random.NextDouble() - 0.5f) * width * 0.6f, radius, height);
            Vector3 outward = -new Vector3(Mathf.Cos(angle), -0.3f, Mathf.Sin(angle));
            Color tone = i % 3 == 0 ? Rock : Snow;
            b.Tri(left, front, peak, tone, outward + (left - right) * 0.01f);
            b.Tri(front, right, peak, tone, outward + (right - left) * 0.01f);
        }
    }

    private static void Trees(SnowBuilder b, Random random)
    {
        int placed = 0;
        for (int attempt = 0; attempt < 2000 && placed < 140; attempt++)
        {
            var at = new Vector2((float)random.NextDouble() * 56 - 28, (float)random.NextDouble() * 56 - 28);
            float r = at.Length();
            if (r < 3 || r > 29 || Mathf.Abs(r - OrbitClearing) < 5.5f)
                continue;
            if (at.DistanceTo(Pond) < 6.5f || at.DistanceTo(Cabin) < 4.5f || at.DistanceTo(new Vector2(-0.5f, 3)) < 2)
                continue;
            float scale = 0.7f + (float)random.NextDouble() * 0.8f;
            Pine(b, new Vector3(at.X, Height(at.X, at.Y) - 0.15f, at.Y), scale, (float)random.NextDouble() * Mathf.Tau);
            placed++;
        }
    }

    /// <summary>A trunk and three cones, each with a snow-capped tip.</summary>
    private static void Pine(SnowBuilder b, Vector3 foot, float scale, float spin)
    {
        const int Sides = 7;
        Prism(b, foot, 0.18f * scale, 0.7f * scale, 5, Bark, spin);
        for (int tier = 0; tier < 3; tier++)
        {
            float radius = (1.25f - tier * 0.3f) * scale;
            float baseY = (0.55f + tier * 0.85f) * scale;
            float height = (1.5f - tier * 0.15f) * scale;
            Vector3 apex = foot + Vector3.Up * (baseY + height);
            for (int s = 0; s < Sides; s++)
            {
                Vector3 b1 = foot + Ring(spin + s * Mathf.Tau / Sides, radius, baseY);
                Vector3 b2 = foot + Ring(spin + (s + 1) * Mathf.Tau / Sides, radius, baseY);
                Vector3 s1 = apex + (b1 - apex) * 0.38f, s2 = apex + (b2 - apex) * 0.38f;
                Vector3 outward = ((b1 + b2) / 2 - new Vector3(foot.X, baseY + foot.Y, foot.Z)) + Vector3.Up * 0.5f;
                b.Tri(apex, s1, s2, Snow, outward);
                b.Quad(s1, b1, b2, s2, Needles, outward);
            }
        }
    }

    private static Vector3 Ring(float angle, float radius, float y) =>
        new(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);

    /// <summary>An upright n-sided prism with a lid.</summary>
    private static void Prism(SnowBuilder b, Vector3 foot, float radius, float height, int sides, Color color, float spin)
    {
        Vector3 top = foot + Vector3.Up * height;
        for (int s = 0; s < sides; s++)
        {
            Vector3 a = Ring(spin + s * Mathf.Tau / sides, radius, 0), c = Ring(spin + (s + 1) * Mathf.Tau / sides, radius, 0);
            b.Quad(foot + a, foot + c, top + c, top + a, color, (a + c) / 2);
            b.Tri(top, top + a, top + c, color, Vector3.Up);
        }
    }

    /// <summary>Log walls, a snowed gable roof, a stone chimney and one warm window.</summary>
    private static void CabinAt(SnowBuilder b, Vector2 at, float turn)
    {
        const float W = 3.2f, D = 2.6f, Wall = 1.9f, Ridge = 1.3f, Eave = 0.35f;
        var origin = new Vector3(at.X, Height(at.X, at.Y) - 0.2f, at.Y);
        var basis = new Basis(Vector3.Up, turn);
        Vector3 P(float x, float y, float z) => origin + basis * new Vector3(x, y, z);
        Vector3 Dir(float x, float y, float z) => basis * new Vector3(x, y, z);

        float w = W / 2, d = D / 2;
        b.Quad(P(-w, 0, d), P(w, 0, d), P(w, Wall, d), P(-w, Wall, d), Wood, Dir(0, 0, 1));
        b.Quad(P(w, 0, -d), P(-w, 0, -d), P(-w, Wall, -d), P(w, Wall, -d), Wood, Dir(0, 0, -1));
        b.Quad(P(w, 0, d), P(w, 0, -d), P(w, Wall, -d), P(w, Wall, d), Wood, Dir(1, 0, 0));
        b.Quad(P(-w, 0, -d), P(-w, 0, d), P(-w, Wall, d), P(-w, Wall, -d), Wood, Dir(-1, 0, 0));
        b.Tri(P(w, Wall, d), P(w, Wall, -d), P(w, Wall + Ridge, 0), Wood, Dir(1, 0, 0));
        b.Tri(P(-w, Wall, -d), P(-w, Wall, d), P(-w, Wall + Ridge, 0), Wood, Dir(-1, 0, 0));

        float rw = w + Eave, rd = d + Eave, drop = Ridge * Eave / d;
        b.Quad(P(-rw, Wall - drop, rd), P(rw, Wall - drop, rd), P(rw, Wall + Ridge, 0), P(-rw, Wall + Ridge, 0), Snow, Dir(0, 1, 1));
        b.Quad(P(rw, Wall - drop, -rd), P(-rw, Wall - drop, -rd), P(-rw, Wall + Ridge, 0), P(rw, Wall + Ridge, 0), Snow, Dir(0, 1, -1));

        b.Quad(P(-1.0f, 0.7f, d + 0.03f), P(-0.3f, 0.7f, d + 0.03f), P(-0.3f, 1.35f, d + 0.03f), P(-1.0f, 1.35f, d + 0.03f), Lamp, Dir(0, 0, 1), emissive: true);
        b.Quad(P(0.4f, 0, d + 0.03f), P(1.1f, 0, d + 0.03f), P(1.1f, 1.5f, d + 0.03f), P(0.4f, 1.5f, d + 0.03f), Bark, Dir(0, 0, 1));
        b.Quad(P(w + 0.03f, 0.7f, -0.35f), P(w + 0.03f, 0.7f, 0.35f), P(w + 0.03f, 1.3f, 0.35f), P(w + 0.03f, 1.3f, -0.35f), Lamp, Dir(1, 0, 0), emissive: true);

        Prism(b, P(-0.9f, Wall, -0.6f), 0.3f, Ridge + 0.6f, 4, Stone, turn + Mathf.Pi / 4);
    }

    /// <summary>Three faceted balls, a carrot and two coal eyes.</summary>
    private static void Snowman(SnowBuilder b, Vector2 at)
    {
        var foot = new Vector3(at.X, Height(at.X, at.Y) - 0.15f, at.Y);
        float y = 0;
        Vector3 head = default;
        foreach (float r in (ReadOnlySpan<float>)[0.75f, 0.55f, 0.38f])
        {
            y += r;
            head = foot + Vector3.Up * y;
            Ball(b, head, r, Snow);
            y += r * 0.75f;
        }

        // Facing the orbit, roughly south-east.
        Vector3 face = new Vector3(0.6f, 0, 0.8f).Normalized();
        Vector3 side = face.Cross(Vector3.Up);
        Vector3 nose = head + face * 0.36f;
        Vector3 tip = nose + face * 0.45f;
        for (int s = 0; s < 4; s++)
        {
            Vector3 a = nose + (side * Mathf.Cos(s * Mathf.Tau / 4) + Vector3.Up * Mathf.Sin(s * Mathf.Tau / 4)) * 0.07f;
            Vector3 c = nose + (side * Mathf.Cos((s + 1) * Mathf.Tau / 4) + Vector3.Up * Mathf.Sin((s + 1) * Mathf.Tau / 4)) * 0.07f;
            b.Tri(a, c, tip, Carrot, (a + c) / 2 - nose);
        }
        foreach (float sign in (ReadOnlySpan<float>)[-1f, 1f])
        {
            Vector3 eye = head + face * 0.35f + side * 0.13f * sign + Vector3.Up * 0.12f;
            b.Quad(eye - side * 0.05f, eye + side * 0.05f, eye + side * 0.05f + Vector3.Up * 0.08f, eye - side * 0.05f + Vector3.Up * 0.08f, Coal, face);
        }
    }

    private static void Ball(SnowBuilder b, Vector3 centre, float radius, Color color)
    {
        const int Rings = 5, Segments = 8;
        Vector3 At(int ring, int seg)
        {
            float lat = Mathf.Pi * ring / Rings - Mathf.Pi / 2;
            float lon = Mathf.Tau * seg / Segments;
            return centre + new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)) * radius;
        }
        for (int ring = 0; ring < Rings; ring++)
        for (int seg = 0; seg < Segments; seg++)
        {
            Vector3 a = At(ring, seg), c = At(ring, seg + 1), d = At(ring + 1, seg + 1), e = At(ring + 1, seg);
            Vector3 outward = (a + c + d + e) / 4 - centre;
            if (ring > 0)
                b.Tri(a, c, d, color, outward);
            if (ring < Rings - 1)
                b.Tri(a, d, e, color, outward);
        }
    }
}

/// <summary>Builds the flake quads once; the flakes shader moves them.</summary>
public partial class SnowFlakes : Node2D
{
    private const int Count = 1400;
    private int[] _indices = [];
    private Vector2[] _points = [];
    private Vector2[] _uvs = [];
    private Color[] _colors = [];

    public static SnowFlakes Build(ShaderMaterial material)
    {
        var random = new Random(11);
        var flakes = new SnowFlakes { Material = material };
        flakes._points = new Vector2[Count * 4];
        flakes._uvs = new Vector2[Count * 4];
        flakes._colors = new Color[Count * 4];
        flakes._indices = new int[Count * 6];
        for (int f = 0; f < Count; f++)
        {
            var seed = new Vector3((float)random.NextDouble() * 26, (float)random.NextDouble() * 14, (float)random.NextDouble() * 26);
            var noise = new Color((float)random.NextDouble(), (float)random.NextDouble(), 0);
            for (int k = 0; k < 4; k++)
            {
                flakes._points[f * 4 + k] = new Vector2(seed.X, seed.Z);
                flakes._uvs[f * 4 + k] = new Vector2(seed.Y, k);
                flakes._colors[f * 4 + k] = noise;
            }
            int v = f * 4, i = f * 6;
            flakes._indices[i] = v;
            flakes._indices[i + 1] = v + 1;
            flakes._indices[i + 2] = v + 3;
            flakes._indices[i + 3] = v;
            flakes._indices[i + 4] = v + 3;
            flakes._indices[i + 5] = v + 2;
        }
        return flakes;
    }

    public override void _Ready() => SnowStage.NeverCull(this);

    public override void _Draw() =>
        RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _indices, _points, _colors, _uvs);
}
