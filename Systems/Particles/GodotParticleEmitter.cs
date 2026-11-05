using System;
using Godot;
using Godot.Collections;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Particles;

/// <summary>
/// Bursts of GPU particles drawn straight through the RenderingServer, set up
/// the way GPUParticles2D does it: a particles RID in 2D mode, a quad mesh as
/// its draw pass, a process material, and the particles added to a canvas item.
/// Each burst is a one-shot particle system restarted at the spot; a small pool
/// per kind lets bursts overlap without cutting each other off, and each
/// burst's canvas item is moved to where it goes off.
/// The kinds a note gives off draw note glyphs out of
/// <see cref="NoteAtlas.Particles"/>: the canvas item's material treats the
/// strip as animation frames and each particle starts on a random one, which
/// it keeps.
/// </summary>
public sealed class GodotParticleEmitter : IParticleEmitter, IDisposable
{
    private const int PoolSize = 10;
    private const float TextureSize = 16f;

    /// <summary>How far from its centre a burst's particles can be drawn; a burst never leaves a screen-sized area.</summary>
    private const float BurstReach = 300f;

    /// <summary>The same for a streak, which is as long as the longest dash.</summary>
    private const float StreakReach = 1400f;

    /// <summary>How far, in degrees, a note glyph may lean from upright.</summary>
    private const float NoteTilt = 25f;

    /// <summary>Note glyphs carry less ink than a square, so they are drawn this much larger.</summary>
    private const float GlyphGrow = 1.5f;

    /// <summary>The half-angle, in degrees, of the cone a burst with a heading is thrown in.</summary>
    private const float HeadedSpread = 55f;

    private readonly Rid _parent;
    private readonly ImageTexture _softDot;
    private readonly GlyphAtlas _notes;
    private readonly CanvasItemMaterial _noteFrames;
    private readonly Rid _quad;
    private readonly bool[] _upright;
    private readonly Pool[] _pools;
    private readonly Resource[] _keepAlive;

    public GodotParticleEmitter(Rid parentCanvas)
    {
        _parent = parentCanvas;
        _softDot = SoftDot();
        _notes = NoteAtlas.Particles;
        _noteFrames = NoteFrames(_notes);
        _quad = Quad();

        _keepAlive = new Resource[] { _softDot, _noteFrames };
        _upright = new[] { false, true, true, true };
        Texture2D notes = _notes.Texture;
        _pools = new[]
        {
            new Pool(Build(LandingMaterial, _softDot, amount: 14, lifetime: 0.55f)),
            new Pool(Build(DashMaterial, notes, amount: 38, lifetime: 0.8f, frames: _noteFrames)),
            new Pool(Build(HitMaterial, notes, amount: 22, lifetime: 0.4f, frames: _noteFrames)),
            new Pool(Build(StreakMaterial, notes, amount: 70, lifetime: 0.5f, explosiveness: 0.85f, reach: StreakReach, frames: _noteFrames)),
        };
    }

    public void Burst(
        Vector2 position,
        ParticleKind kind,
        Color tint,
        Vector2 area = default,
        float angle = 0f,
        Vector2 heading = default)
    {
        Pool pool = _pools[(int)kind];
        Emitter emitter = pool.Emitters[pool.Next];
        pool.Next = (pool.Next + 1) % pool.Emitters.Length;

        // Each pooled burst has its own material, so bursts set off together
        // keep their own boxes and headings.
        ParticleProcessMaterial material = emitter.Material;
        material.EmissionBoxExtents = new Vector3(area.X / 2, area.Y / 2, 0);

        // A heading narrows the burst to a cone thrown that way; it is given on
        // screen, and the burst's directions are local to its turned item.
        if (heading != Vector2.Zero)
        {
            Vector2 local = heading.Normalized().Rotated(-angle);
            material.Direction = new Vector3(local.X, local.Y, 0);
            material.Spread = HeadedSpread;
        }
        else
        {
            material.Direction = emitter.Direction;
            material.Spread = emitter.Spread;
        }

        // Glyphs lean a little either way of upright, whichever way the burst is turned.
        if (_upright[(int)kind])
        {
            float facing = -Mathf.RadToDeg(angle);
            material.AngleMin = facing - NoteTilt;
            material.AngleMax = facing + NoteTilt;
        }

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
                emitter.Material.Dispose();
            }
        }

        RenderingServer.FreeRid(_quad);
        foreach (Resource resource in _keepAlive)
            resource.Dispose();
    }

    private Emitter[] Build(
        Func<ParticleProcessMaterial> makeMaterial,
        Texture2D texture,
        int amount,
        float lifetime,
        float explosiveness = 1f,
        float reach = BurstReach,
        CanvasItemMaterial? frames = null)
    {
        var emitters = new Emitter[PoolSize];
        for (int i = 0; i < emitters.Length; i++)
        {
            ParticleProcessMaterial material = makeMaterial();
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
            if (frames is not null)
            {
                RenderingServer.CanvasItemSetMaterial(item, frames.GetRid());
                RenderingServer.CanvasItemSetDefaultTextureFilter(
                    item, RenderingServer.CanvasItemTextureFilter.LinearWithMipmaps);
            }

            emitters[i] = new Emitter(particles, item, material, material.Direction, material.Spread);
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
    /// Spray: note glyphs burst out in every direction from the
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
        ScaleMin = 1.2f * GlyphGrow,
        ScaleMax = 2.8f * GlyphGrow,
        ScaleCurve = Shrink(),
        AngleMin = -NoteTilt,
        AngleMax = NoteTilt,
        AnimOffsetMax = 1f,
        Color = Colors.White,
    };

    /// <summary>
    /// Sparks: fast note glyphs thrown out from the box in every direction that
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
        ScaleMin = 0.9f * GlyphGrow,
        ScaleMax = 2f * GlyphGrow,
        ScaleCurve = Shrink(),
        AngleMin = -NoteTilt,
        AngleMax = NoteTilt,
        AnimOffsetMax = 1f,
        Color = Colors.White,
    };

    /// <summary>
    /// A dash's streak: note glyphs laid along the box, which lies on
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
        ScaleMin = 0.5f * GlyphGrow,
        ScaleMax = 1.5f * GlyphGrow,
        ScaleCurve = Shrink(),
        AngleMin = -NoteTilt,
        AngleMax = NoteTilt,
        AnimOffsetMax = 1f,
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

    /// <summary>
    /// The canvas item material that draws one cell of the note strip per
    /// particle: the process material's animation offset, random from 0 to 1,
    /// picks the cell, and with no animation speed the particle keeps it.
    /// </summary>
    private static CanvasItemMaterial NoteFrames(GlyphAtlas atlas) => new()
    {
        ParticlesAnimation = true,
        ParticlesAnimHFrames = atlas.Count,
        ParticlesAnimVFrames = 1,
        ParticlesAnimLoop = false,
    };

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

    /// <summary>One pooled burst; <see cref="Direction"/> and <see cref="Spread"/> are its kind's own, for bursts with no heading.</summary>
    private readonly record struct Emitter(
        Rid Particles,
        Rid Item,
        ParticleProcessMaterial Material,
        Vector3 Direction,
        float Spread);

    private sealed class Pool(Emitter[] emitters)
    {
        public Emitter[] Emitters { get; } = emitters;
        public int Next { get; set; }
    }
}
