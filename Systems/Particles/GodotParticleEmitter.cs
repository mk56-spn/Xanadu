using System;
using Godot;
using Godot.Collections;
using Xanadu.Ecs;

namespace Xanadu.Systems.Particles;

/// <summary>
/// Bursts of GPU particles drawn straight through the RenderingServer, set up
/// the way GPUParticles2D does it: a particles RID in 2D mode, a quad mesh as
/// its draw pass, a process material, and the particles added to a canvas item.
/// Each burst is a one-shot particle system restarted at the spot; a small pool
/// per kind lets bursts overlap without cutting each other off, and each
/// burst's canvas item is moved to where it goes off.
/// </summary>
public sealed class GodotParticleEmitter : IParticleEmitter, IDisposable
{
    private const int PoolSize = 10;
    private const float TextureSize = 16f;

    /// <summary>How far from its centre a burst's particles can be drawn; a burst never leaves a screen-sized area.</summary>
    private const float BurstReach = 300f;

    /// <summary>The same for a streak, which is as long as the longest dash.</summary>
    private const float StreakReach = 1400f;

    private readonly Rid _parent;
    private readonly ImageTexture _softDot;
    private readonly ImageTexture _solidSquare;
    private readonly Rid _quad;
    private readonly ParticleProcessMaterial[] _materials;
    private readonly Pool[] _pools;
    private readonly Resource[] _keepAlive;

    public GodotParticleEmitter(Rid parentCanvas)
    {
        _parent = parentCanvas;
        _softDot = SoftDot();
        _solidSquare = SolidSquare();
        _quad = Quad();

        ParticleProcessMaterial landing = LandingMaterial();
        ParticleProcessMaterial dash = DashMaterial();
        ParticleProcessMaterial hit = HitMaterial();
        ParticleProcessMaterial streak = StreakMaterial();
        _keepAlive = new Resource[] { _softDot, _solidSquare, landing, dash, hit, streak };
        _materials = new[] { landing, dash, hit, streak };
        _pools = new[]
        {
            new Pool(Build(landing, _softDot, amount: 14, lifetime: 0.55f)),
            new Pool(Build(dash, _solidSquare, amount: 38, lifetime: 0.8f)),
            new Pool(Build(hit, _solidSquare, amount: 22, lifetime: 0.4f)),
            new Pool(Build(streak, _solidSquare, amount: 70, lifetime: 0.5f, explosiveness: 0.85f, reach: StreakReach)),
        };
    }

    public void Burst(Vector2 position, ParticleKind kind, Color tint, Vector2 area = default, float angle = 0f)
    {
        // Only where particles start: bursts already in flight keep their places.
        _materials[(int)kind].EmissionBoxExtents = new Vector3(area.X / 2, area.Y / 2, 0);

        Pool pool = _pools[(int)kind];
        Emitter emitter = pool.Emitters[pool.Next];
        pool.Next = (pool.Next + 1) % pool.Emitters.Length;

        // The burst is local to its canvas item, which sits where it goes off.
        RenderingServer.CanvasItemSetTransform(emitter.Item, new Transform2D(angle, position));
        RenderingServer.CanvasItemSetModulate(emitter.Item, tint);
        RenderingServer.ParticlesRestart(emitter.Particles);
        RenderingServer.ParticlesSetEmitting(emitter.Particles, true);
    }

    public void Dispose()
    {
        foreach (Pool pool in _pools)
        {
            foreach (Emitter emitter in pool.Emitters)
            {
                RenderingServer.FreeRid(emitter.Item);
                RenderingServer.FreeRid(emitter.Particles);
            }
        }

        RenderingServer.FreeRid(_quad);
        foreach (Resource resource in _keepAlive)
            resource.Dispose();
    }

    private Emitter[] Build(
        ParticleProcessMaterial material,
        ImageTexture texture,
        int amount,
        float lifetime,
        float explosiveness = 1f,
        float reach = BurstReach)
    {
        var emitters = new Emitter[PoolSize];
        for (int i = 0; i < emitters.Length; i++)
        {
            Rid particles = RenderingServer.ParticlesCreate();
            RenderingServer.ParticlesSetMode(particles, RenderingServer.ParticlesMode.Mode2D);
            RenderingServer.ParticlesSetDrawPasses(particles, 1);
            RenderingServer.ParticlesSetDrawPassMesh(particles, 0, _quad);
            RenderingServer.ParticlesSetAmount(particles, amount);
            RenderingServer.ParticlesSetLifetime(particles, lifetime);
            RenderingServer.ParticlesSetOneShot(particles, true);
            RenderingServer.ParticlesSetExplosivenessRatio(particles, explosiveness);
            RenderingServer.ParticlesSetRandomnessRatio(particles, 0.4f);
            RenderingServer.ParticlesSetUseLocalCoordinates(particles, true);
            RenderingServer.ParticlesSetFixedFps(particles, 60);
            RenderingServer.ParticlesSetFractionalDelta(particles, true);
            RenderingServer.ParticlesSetProcessMaterial(particles, material.GetRid());

            // The server culls by this box, around the burst.
            RenderingServer.ParticlesSetCustomAabb(
                particles,
                new Aabb(new Vector3(-reach, -reach, -1), new Vector3(reach * 2, reach * 2, 2)));

            // Not emitting until the first burst: a one-shot system starts out running.
            RenderingServer.ParticlesSetEmitting(particles, false);

            Rid item = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(item, _parent);
            RenderingServer.CanvasItemAddParticles(item, particles, texture.GetRid());
            emitters[i] = new Emitter(particles, item);
        }

        return emitters;
    }

    /// <summary>Dust: thrown up and out from the feet, then pulled down.</summary>
    private static ParticleProcessMaterial LandingMaterial() => new()
    {
        ParticleFlagDisableZ = true,
        Direction = new Vector3(0, -1, 0),
        Spread = 85f,
        InitialVelocityMin = 70f,
        InitialVelocityMax = 190f,
        Gravity = new Vector3(0, 220, 0),
        DampingMin = 60f,
        DampingMax = 120f,
        ScaleMin = 0.8f,
        ScaleMax = 1.7f,
        ColorRamp = FadeOut(new Color(0.86f, 0.9f, 1f, 0.85f)),
    };

    /// <summary>
    /// Spray: solid squares at any angle burst out in every direction from the
    /// box, then fall and shrink away; white, so the burst's tint gives them the note's colour.
    /// </summary>
    private static ParticleProcessMaterial DashMaterial() => new()
    {
        ParticleFlagDisableZ = true,
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
        Direction = new Vector3(0, -1, 0),
        Spread = 180f,
        InitialVelocityMin = 180f,
        InitialVelocityMax = 460f,
        Gravity = new Vector3(0, 1100, 0),
        DampingMin = 40f,
        DampingMax = 90f,
        ScaleMin = 1.2f,
        ScaleMax = 2.8f,
        ScaleCurve = Shrink(),
        AngleMin = 0f,
        AngleMax = 360f,
        Color = Colors.White,
    };

    /// <summary>
    /// Sparks: fast, solid squares at any angle thrown out from the box in every direction that
    /// brake hard and shrink away; white, so the burst's tint gives them the note's colour.
    /// </summary>
    private static ParticleProcessMaterial HitMaterial() => new()
    {
        ParticleFlagDisableZ = true,
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
        Direction = new Vector3(0, -1, 0),
        Spread = 180f,
        InitialVelocityMin = 260f,
        InitialVelocityMax = 620f,
        Gravity = new Vector3(0, 300, 0),
        DampingMin = 500f,
        DampingMax = 900f,
        ScaleMin = 0.9f,
        ScaleMax = 2f,
        ScaleCurve = Shrink(),
        AngleMin = 0f,
        AngleMax = 360f,
        Color = Colors.White,
    };

    /// <summary>
    /// A dash's streak: squares at any angle laid along the box, which lies on
    /// the path, drifting back the way the player came and spreading a little
    /// sideways as they brake, shrink and fade; white, so the tint gives them
    /// the note's colour.
    /// </summary>
    private static ParticleProcessMaterial StreakMaterial() => new()
    {
        ParticleFlagDisableZ = true,
        EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
        Direction = new Vector3(-1, 0, 0),
        Spread = 35f,
        InitialVelocityMin = 40f,
        InitialVelocityMax = 220f,
        Gravity = new Vector3(0, 0, 0),
        DampingMin = 180f,
        DampingMax = 360f,
        ScaleMin = 0.5f,
        ScaleMax = 1.5f,
        ScaleCurve = Shrink(),
        AngleMin = 0f,
        AngleMax = 360f,
        ColorRamp = FadeOut(Colors.White),
    };

    /// <summary>Full size at birth, nothing at death.</summary>
    private static CurveTexture Shrink()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0f, 1f));
        curve.AddPoint(new Vector2(1f, 0f));
        return new CurveTexture { Curve = curve };
    }

    private static GradientTexture1D FadeOut(Color color)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, color);
        gradient.SetColor(1, new Color(color, 0f));
        return new GradientTexture1D { Gradient = gradient };
    }

    /// <summary>A white dot with soft edges; the process material colours and scales it.</summary>
    private static ImageTexture SoftDot()
    {
        int size = (int)TextureSize;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x + 0.5f - radius, y + 0.5f - radius).Length() / radius;
                float alpha = Mathf.Clamp(1f - distance, 0f, 1f);
                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
            }
        }

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A white square with a crisp edge, drawn large so it stays crisp when scaled up and turned.</summary>
    private static ImageTexture SolidSquare()
    {
        const int size = 64;
        const int inset = 4;
        Image image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(Colors.Transparent);
        // The quad turns with the particle; the clear rim keeps the turned edge from looking jagged.
        image.FillRect(new Rect2I(inset, inset, size - inset * 2, size - inset * 2), Colors.White);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>The draw pass: one quad the size of the texture, centred on the particle.</summary>
    private static Rid Quad()
    {
        float half = TextureSize / 2f;
        Rid mesh = RenderingServer.MeshCreate();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)RenderingServer.ArrayType.Max);
        arrays[(int)RenderingServer.ArrayType.Vertex] = new[]
        {
            new Vector2(-half, -half), new Vector2(half, -half),
            new Vector2(half, half), new Vector2(-half, half),
        };
        arrays[(int)RenderingServer.ArrayType.TexUV] = new[]
        {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
        };
        arrays[(int)RenderingServer.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        RenderingServer.MeshAddSurfaceFromArrays(
            mesh,
            RenderingServer.PrimitiveType.Triangles,
            arrays,
            new Godot.Collections.Array(),
            new Dictionary(),
            RenderingServer.ArrayFormat.FlagUse2DVertices);
        return mesh;
    }

    private readonly record struct Emitter(Rid Particles, Rid Item);

    private sealed class Pool(Emitter[] emitters)
    {
        public Emitter[] Emitters { get; } = emitters;
        public int Next { get; set; }
    }
}
