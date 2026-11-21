using System.Collections.Generic;
using Xanadu.Core.Maths;

namespace Xanadu.Labs.Experiments.Particles;

/// <summary>Where an emitter's particles start: <see cref="EmitterSettings.Size"/> says how big.</summary>
internal enum EmitterShape
{
    Point,
    Line,
    Circle,
    Disc,
    Box,
    Ring,
}

/// <summary>How a particle is drawn: every look stretches along the velocity when the emitter asks.</summary>
internal enum ParticleLook
{
    Glow,
    Dot,
    Ring,
    Spark,
    Square,
}

/// <summary>What a module does to the particles it reaches; the numbers it reads are its <see cref="ModuleKinds"/> entry's.</summary>
internal enum ModuleKind
{
    Force = 1,
    Drag,
    Noise,
    Attract,
    Vortex,
    Pointer,
    FieldSlope,
    FieldSteer,
    Bounds,
    Speed,
}

/// <summary>
/// One emitter: a shape it spawns on, how many a second (and in bursts), what
/// speed and way they leave in, how long they live and how they look across
/// that life. An emitter with no rate still spawns as another's child: every
/// particle of a parent that dies of old age bursts <see cref="Children"/> of it.
/// Positions are px from the view's middle, y down, on a 1280 × 720 world.
/// </summary>
internal sealed class EmitterSettings
{
    public bool On;
    public EmitterShape Shape;
    public Vector2 Position;
    public Vector2 Size = new(40, 40);
    public float Turn;

    public float Rate;
    public uint Burst;

    /// <summary>Seconds between bursts; 0 bursts once, at the start.</summary>
    public float BurstEvery;

    /// <summary>Degrees; 0 is right, -90 up.</summary>
    public float Direction = -90;
    public float Spread = 30;
    public float Outward;

    /// <summary>Share of the speed sent round the shape's middle instead (orbits, spirals).</summary>
    public float Tangent;
    public float Inherit;
    public Vector2 Speed = new(80, 160);
    public Vector2 Life = new(1, 2);

    public Vector2 SizeRange = new(6, 2);
    public float Jitter;
    public float Stretch;
    public ParticleLook Look;
    public Color Born = new(1, 1, 1, 1);
    public Color Dies = new(1, 1, 1, 0);
    public float HueJitter;
    public float FadeIn;

    public uint Trail;
    public float TrailWidth = 0.8f;
    public float Deposit;

    /// <summary>The emitter every death of this one's particles bursts, or -1.</summary>
    public int Child = -1;
    public uint Children = 40;

    public EmitterSettings Copy() => (EmitterSettings)MemberwiseClone();
}

/// <summary>
/// One step of the stack every particle goes through each frame: a kind, which
/// emitters it reaches, a mode, and eight numbers whose meaning the kind gives
/// (<see cref="ModuleKinds"/>).
/// </summary>
internal sealed class ModuleSettings(ModuleKind kind)
{
    public ModuleKind Kind = kind;
    public bool On = true;

    /// <summary>Bit i reaches emitter i.</summary>
    public uint Mask = 0b1111;
    public int Mode;
    public readonly float[] Values = ModuleKinds.Of(kind).Defaults();

    public ModuleSettings Copy()
    {
        var copy = new ModuleSettings(Kind) { On = On, Mask = Mask, Mode = Mode };
        Values.CopyTo(copy.Values, 0);
        return copy;
    }
}

/// <summary>What the whole show shares: time, blending, the trail ring's pace and the field.</summary>
internal sealed class WorldSettings
{
    public float TimeScale = 1;
    public bool Additive = true;

    /// <summary>Frames between trail samples: a longer gap, a longer trail from the same samples.</summary>
    public uint TrailEvery = 1;

    /// <summary>How fast the field fades, per second.</summary>
    public float FieldFade = 1;

    /// <summary>How much of a cell spreads into its neighbours each frame, 0..1.</summary>
    public float FieldSpread = 0.2f;
    public float FieldGain = 0;
    public Color FieldTint = new(0.3f, 0.9f, 0.6f, 1);
    public Color Background = new(0.02f, 0.02f, 0.035f, 1);

    public WorldSettings Copy() => (WorldSettings)MemberwiseClone();
}

/// <summary>A whole show: the world, four emitters (A to D) and the module stack, in order.</summary>
internal sealed class ParticleScene
{
    public const int EmitterCount = 4;
    public const int MostModules = 12;

    public string Name = "";
    public WorldSettings World = new();
    public EmitterSettings[] Emitters = [new(), new(), new(), new()];
    public List<ModuleSettings> Modules = new();

    public ParticleScene Copy()
    {
        var copy = new ParticleScene { Name = Name, World = World.Copy() };
        for (int i = 0; i < EmitterCount; i++)
            copy.Emitters[i] = Emitters[i].Copy();
        foreach (ModuleSettings module in Modules)
            copy.Modules.Add(module.Copy());
        return copy;
    }
}
