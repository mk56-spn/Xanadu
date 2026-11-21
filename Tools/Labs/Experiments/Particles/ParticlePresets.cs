using System;
using Xanadu.Core.Maths;

namespace Xanadu.Labs.Experiments.Particles;

/// <summary>
/// Starting points, as data: each fills the same emitters, modules and world
/// the panel edits, so nothing here is code a scene owns. Pick one, then change
/// anything. The world is 1280 × 720 px, centred, y down.
/// </summary>
internal static class ParticlePresets
{
    public static readonly (string Name, Func<ParticleScene> Make)[] All =
    [
        ("FOUNTAIN", Fountain),
        ("FIREWORKS", Fireworks),
        ("PHYSARUM", Physarum),
        ("SMOKE", Smoke),
        ("GALAXY", Galaxy),
        ("SWARM", Swarm),
        ("SNOW", Snow),
    ];

    public static string[] Names => Array.ConvertAll(All, p => p.Name);

    private static ModuleSettings Module(ModuleKind kind, uint mask = 0b1111, int mode = 0, params (int Slot, float Value)[] set)
    {
        var module = new ModuleSettings(kind) { Mask = mask, Mode = mode };
        foreach ((int slot, float value) in set)
            module.Values[slot] = value;
        return module;
    }

    private static ParticleScene Fountain()
    {
        var s = new ParticleScene { Name = "FOUNTAIN" };
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Point, Position = new(0, 300), Rate = 2500,
            Direction = -90, Spread = 18, Speed = new(520, 640), Life = new(2.2f, 3), SizeRange = new(5, 2),
            Look = ParticleLook.Glow, Born = new(0.5f, 0.8f, 1, 1), Dies = new(0.1f, 0.3f, 1, 0), HueJitter = 0.04f,
            Trail = 6, TrailWidth = 0.6f, Stretch = 0.01f,
        };
        s.Modules.Add(Module(ModuleKind.Force, set: [(1, 520)]));
        s.Modules.Add(Module(ModuleKind.Drag, set: [(0, 0.15f)]));
        s.Modules.Add(Module(ModuleKind.Bounds, set: [(0, 0.35f), (1, 0.2f)]));
        s.Modules.Add(Module(ModuleKind.Pointer, mode: 0));
        return s;
    }

    private static ParticleScene Fireworks()
    {
        var s = new ParticleScene { Name = "FIREWORKS" };
        s.World.TrailEvery = 2;
        // A: rockets, which die into B's shells.
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Line, Position = new(0, 360), Size = new(900, 0), Rate = 1.6f,
            Direction = -90, Spread = 12, Speed = new(560, 700), Life = new(1.1f, 1.5f), SizeRange = new(5, 4),
            Look = ParticleLook.Glow, Born = new(1, 0.75f, 0.4f, 1), Dies = new(1, 0.5f, 0.2f, 1),
            Trail = 12, TrailWidth = 0.5f, Child = 1, Children = 220,
        };
        // B: the shells, sparks that each crackle into C at the end.
        s.Emitters[1] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Point, Spread = 360, Speed = new(40, 300), Life = new(1.3f, 2.2f),
            SizeRange = new(4, 1.5f), Look = ParticleLook.Glow, Born = new(1, 0.35f, 0.6f, 1), Dies = new(0.4f, 0.1f, 1, 0),
            HueJitter = 0.5f, Inherit = 0.2f, Trail = 8, TrailWidth = 0.7f, Child = 2, Children = 2,
        };
        s.Emitters[2] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Point, Spread = 360, Speed = new(10, 60), Life = new(0.15f, 0.4f),
            SizeRange = new(5, 0), Look = ParticleLook.Spark, Born = new(1, 1, 0.85f, 1), Dies = new(1, 0.9f, 0.6f, 0),
        };
        s.Modules.Add(Module(ModuleKind.Force, 0b111, set: [(1, 200)]));
        s.Modules.Add(Module(ModuleKind.Drag, 0b110, set: [(0, 1.1f)]));
        s.Modules.Add(Module(ModuleKind.Bounds, 0b111, 2, set: [(2, -80), (6, 0)]));
        return s;
    }

    private static ParticleScene Physarum()
    {
        var s = new ParticleScene { Name = "PHYSARUM" };
        s.World.FieldFade = 5;
        s.World.FieldSpread = 0.6f;
        s.World.FieldGain = 0.25f;
        s.World.FieldTint = new Color(1, 0.75f, 0.3f, 1);
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Disc, Size = new(340, 340), Burst = 150000, Spread = 360,
            Speed = new(60, 60), Life = new(100000, 100000), SizeRange = new(1.2f, 1.2f), Look = ParticleLook.Dot,
            Born = new(1, 0.95f, 0.85f, 0.12f), Dies = new(1, 0.95f, 0.85f, 0.12f), Deposit = 5,
        };
        s.Modules.Add(Module(ModuleKind.FieldSteer, set: [(0, 10), (1, 0.7f), (2, 45), (3, 60), (4, 2)]));
        s.Modules.Add(Module(ModuleKind.Bounds, mode: 1));
        s.Modules.Add(Module(ModuleKind.Pointer, mode: 0, set: [(0, 9000), (1, 90), (4, 1)]));
        return s;
    }

    private static ParticleScene Smoke()
    {
        var s = new ParticleScene { Name = "SMOKE" };
        s.World.Additive = false;
        s.World.FieldFade = 2;
        s.World.FieldSpread = 0.5f;
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Box, Position = new(0, 330), Size = new(120, 16), Rate = 320,
            Direction = -90, Spread = 30, Speed = new(30, 90), Life = new(4, 6), SizeRange = new(8, 46), Jitter = 0.4f,
            Look = ParticleLook.Glow, Born = new(0.75f, 0.72f, 0.7f, 0.14f), Dies = new(0.4f, 0.4f, 0.45f, 0), FadeIn = 0.4f,
            Deposit = 1,
        };
        s.Modules.Add(Module(ModuleKind.Force, set: [(1, -70)]));
        s.Modules.Add(Module(ModuleKind.Noise, mode: 0, set: [(0, 90), (1, 260), (2, 0.15f)]));
        s.Modules.Add(Module(ModuleKind.FieldSlope, set: [(0, -2500), (1, 10)]));
        s.Modules.Add(Module(ModuleKind.Drag, set: [(0, 0.6f)]));
        s.Modules.Add(Module(ModuleKind.Pointer, mode: 2, set: [(0, 2500), (1, 200)]));
        return s;
    }

    private static ParticleScene Galaxy()
    {
        var s = new ParticleScene { Name = "GALAXY" };
        s.World.TrailEvery = 2;
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Ring, Size = new(60, 340), Rate = 4000, Tangent = 1, Speed = new(120, 200),
            Life = new(5, 9), SizeRange = new(2, 1), Look = ParticleLook.Glow, Born = new(0.6f, 0.7f, 1, 0.25f),
            Dies = new(1, 0.5f, 0.8f, 0), HueJitter = 0.12f, FadeIn = 0.6f, Trail = 5, TrailWidth = 0.5f,
        };
        s.Emitters[1] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Disc, Size = new(70, 70), Rate = 250, Tangent = 1, Speed = new(60, 120),
            Life = new(1.5f, 3), SizeRange = new(3, 1), Look = ParticleLook.Glow, Born = new(1, 0.85f, 0.6f, 0.5f),
            Dies = new(1, 0.6f, 0.3f, 0), FadeIn = 0.3f,
        };
        s.Modules.Add(Module(ModuleKind.Vortex, set: [(2, 220), (3, 0), (4, -30)]));
        s.Modules.Add(Module(ModuleKind.Attract, set: [(2, 1100), (4, 180)]));
        s.Modules.Add(Module(ModuleKind.Noise, mode: 0, set: [(0, 50), (1, 120), (2, 0.1f)]));
        s.Modules.Add(Module(ModuleKind.Drag, set: [(0, 0.05f)]));
        return s;
    }

    private static ParticleScene Swarm()
    {
        var s = new ParticleScene { Name = "SWARM" };
        s.World.FieldFade = 3;
        s.World.FieldSpread = 0.4f;
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Disc, Size = new(220, 220), Burst = 30000, Spread = 360,
            Speed = new(20, 80), Life = new(100000, 100000), SizeRange = new(2.5f, 2.5f), Stretch = 0.02f, Look = ParticleLook.Dot,
            Born = new(0.3f, 1, 0.8f, 0.8f), Dies = new(0.3f, 1, 0.8f, 0.8f), HueJitter = 0.15f, Deposit = 2,
        };
        s.Modules.Add(Module(ModuleKind.Noise, mode: 0, set: [(0, 160), (1, 220), (2, 0.4f)]));
        s.Modules.Add(Module(ModuleKind.FieldSlope, set: [(0, -2500), (1, 6)]));
        s.Modules.Add(Module(ModuleKind.Attract, set: [(2, 900), (4, 260)]));
        s.Modules.Add(Module(ModuleKind.Pointer, mode: 1, set: [(0, 6000), (1, 300)]));
        s.Modules.Add(Module(ModuleKind.Speed, set: [(0, 40), (1, 320)]));
        s.Modules.Add(Module(ModuleKind.Bounds, set: [(0, 0.8f)]));
        return s;
    }

    private static ParticleScene Snow()
    {
        var s = new ParticleScene { Name = "SNOW" };
        s.World.Additive = false;
        s.Emitters[0] = new EmitterSettings
        {
            On = true, Shape = EmitterShape.Line, Position = new(0, -380), Size = new(1400, 0), Rate = 400,
            Direction = 90, Spread = 20, Speed = new(30, 70), Life = new(30, 30), SizeRange = new(4, 4), Jitter = 0.6f,
            Look = ParticleLook.Dot, Born = new(1, 1, 1, 0.9f), Dies = new(1, 1, 1, 0.9f), FadeIn = 1,
        };
        s.Modules.Add(Module(ModuleKind.Noise, mode: 1, set: [(0, 60), (1, 200), (2, 0.2f), (4, 1)]));
        s.Modules.Add(Module(ModuleKind.Force, set: [(0, 0), (1, 50)]));
        s.Modules.Add(Module(ModuleKind.Bounds, mode: 1, set: [(6, 0), (7, 0)]));
        s.Modules.Add(Module(ModuleKind.Bounds, mode: 2, set: [(2, -40), (4, 0), (5, 0), (6, 0)]));
        s.Modules.Add(Module(ModuleKind.Pointer, mode: 0, set: [(0, 3000), (1, 140)]));
        return s;
    }
}
