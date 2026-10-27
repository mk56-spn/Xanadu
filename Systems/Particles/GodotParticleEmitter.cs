using System;
using Godot;
using Godot.Collections;
using Writer.Ecs;

namespace Writer.Systems.Particles;

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
    private const int PoolSize = 6;
    private const float TextureSize = 16f;

    private readonly Rid _parent;
    private readonly ImageTexture _texture;
    private readonly Rid _quad;
    private readonly Pool[] _pools;
    private readonly Resource[] _keepAlive;

    public GodotParticleEmitter(Rid parentCanvas)
    {
        _parent = parentCanvas;
        _texture = SoftDot();
        _quad = Quad();

        ParticleProcessMaterial landing = LandingMaterial();
        ParticleProcessMaterial dash = DashMaterial();
        _keepAlive = new Resource[] { _texture, landing, dash };
        _pools = new[]
        {
            new Pool(Build(landing, amount: 14, lifetime: 0.55f)),
            new Pool(Build(dash, amount: 18, lifetime: 0.45f)),
        };
    }

    public void Burst(Vector2 position, ParticleKind kind)
    {
        Pool pool = _pools[(int)kind];
        Emitter emitter = pool.Emitters[pool.Next];
        pool.Next = (pool.Next + 1) % pool.Emitters.Length;

        // The burst is local to its canvas item, which sits where it goes off.
        RenderingServer.CanvasItemSetTransform(emitter.Item, new Transform2D(0f, position));
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

    private Emitter[] Build(ParticleProcessMaterial material, int amount, float lifetime)
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
            RenderingServer.ParticlesSetExplosivenessRatio(particles, 1f);
            RenderingServer.ParticlesSetRandomnessRatio(particles, 0.4f);
            RenderingServer.ParticlesSetUseLocalCoordinates(particles, true);
            RenderingServer.ParticlesSetFixedFps(particles, 60);
            RenderingServer.ParticlesSetFractionalDelta(particles, true);
            RenderingServer.ParticlesSetProcessMaterial(particles, material.GetRid());

            // The server culls by this box, around the burst; one never leaves a screen-sized area.
            RenderingServer.ParticlesSetCustomAabb(
                particles,
                new Aabb(new Vector3(-300, -300, -1), new Vector3(600, 600, 2)));

            // Not emitting until the first burst: a one-shot system starts out running.
            RenderingServer.ParticlesSetEmitting(particles, false);

            Rid item = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(item, _parent);
            RenderingServer.CanvasItemAddParticles(item, particles, _texture.GetRid());
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

    /// <summary>Spray: bursts out in every direction and slows to a stop.</summary>
    private static ParticleProcessMaterial DashMaterial() => new()
    {
        ParticleFlagDisableZ = true,
        Direction = new Vector3(0, -1, 0),
        Spread = 180f,
        InitialVelocityMin = 140f,
        InitialVelocityMax = 340f,
        Gravity = Vector3.Zero,
        DampingMin = 160f,
        DampingMax = 320f,
        ScaleMin = 0.6f,
        ScaleMax = 1.4f,
        ColorRamp = FadeOut(new Color(0.45f, 0.85f, 1f, 0.95f)),
    };

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
