using System.Collections.Generic;
using Godot;

namespace Xanadu.Particles;

/// <summary>What each particle of an effect is drawn with.</summary>
public enum XpfTexture
{
    /// <summary>A white dot with soft edges.</summary>
    SoftDot,

    /// <summary>One note glyph per particle, picked at random from the note strip and kept.</summary>
    NoteGlyphs,
}

/// <summary>Where particles start.</summary>
public enum XpfShape
{
    /// <summary>All at the emitter's spot.</summary>
    Point,

    /// <summary>Anywhere in a box around the spot; a burst's own area replaces <see cref="XpfParticle.BoxSize"/>.</summary>
    Box,
}

/// <summary>A point on a scale curve: <see cref="Value"/> at <see cref="Time"/> of a particle's life, both 0 to 1.</summary>
public readonly record struct XpfCurvePoint(float Time, float Value);

/// <summary>A stop on a colour ramp at <see cref="Time"/> of a particle's life, 0 to 1.</summary>
public readonly record struct XpfColorStop(float Time, Color Color);

/// <summary>
/// One particle effect: how many particles, how long they live, how they are
/// thrown, pulled, braked and scaled, and what they look like. Whoever plays it
/// decides whether it goes off once (a burst) or keeps flowing (a stream).
/// Angles are in degrees on screen: 0 points right, 90 points down.
/// </summary>
public sealed class XpfParticle
{
    public const int MaxNameLength = 64;
    public const int MaxAmount = 4096;
    public const int MaxCurvePoints = 16;

    public string Name = "particle";
    public XpfTexture Texture = XpfTexture.SoftDot;

    /// <summary>Particles alive at once; for a burst, how many it throws.</summary>
    public int Amount = 16;

    public float Lifetime = 0.6f;

    /// <summary>0 spreads births over the lifetime, 1 throws them all at once.</summary>
    public float Explosiveness = 1f;

    /// <summary>How far each particle's lifetime may wander from <see cref="Lifetime"/>, 0 to 1.</summary>
    public float Randomness = 0.4f;

    public XpfShape Shape = XpfShape.Point;

    /// <summary>The box particles start in when the player gives no area of its own.</summary>
    public Vector2 BoxSize = Vector2.Zero;

    /// <summary>The way particles are thrown, in degrees.</summary>
    public float Direction = -90f;

    /// <summary>Half the cone particles are thrown in, in degrees; 180 is every way.</summary>
    public float Spread = 45f;

    /// <summary>The half-cone used instead of <see cref="Spread"/> when a burst is thrown along a heading.</summary>
    public float HeadedSpread = 55f;

    public float SpeedMin = 50f;
    public float SpeedMax = 150f;
    public Vector2 Gravity = Vector2.Zero;
    public float DampingMin;
    public float DampingMax;
    public float ScaleMin = 1f;
    public float ScaleMax = 1f;

    /// <summary>The particle's size over its life; empty keeps it constant.</summary>
    public List<XpfCurvePoint> ScaleCurve = new();

    /// <summary>The range each particle is turned by at birth, in degrees.</summary>
    public float AngleMin;
    public float AngleMax;

    /// <summary>Keeps <see cref="AngleMin"/>..<see cref="AngleMax"/> relative to upright whichever way the emitter is turned.</summary>
    public bool Upright;

    /// <summary>The particles' colour, multiplied by <see cref="Ramp"/> and the player's tint.</summary>
    public Color Color = Colors.White;

    /// <summary>The colour over a particle's life, multiplying <see cref="Color"/>; empty keeps it constant.</summary>
    public List<XpfColorStop> Ramp = new();

    /// <summary>How far from the emitter particles can be drawn; the renderer culls outside it.</summary>
    public float Reach = 300f;

    public XpfParticle Clone()
    {
        var copy = (XpfParticle)MemberwiseClone();
        copy.ScaleCurve = new List<XpfCurvePoint>(ScaleCurve);
        copy.Ramp = new List<XpfColorStop>(Ramp);
        return copy;
    }
}

/// <summary>Every particle effect the game, the animator and levels can use, by name.</summary>
public sealed class XpfLibrary
{
    public const ushort MajorVersion = 0;
    public const ushort MinorVersion = 1;

    public List<XpfParticle> Particles = new();

    /// <summary>The effect called <paramref name="name"/>, or null.</summary>
    public XpfParticle? Find(string name) => Particles.Find(particle => particle.Name == name);

    /// <summary><paramref name="baseName"/>, or the first of <c>baseName-2</c>, <c>baseName-3</c>... not yet used.</summary>
    public string UniqueName(string baseName)
    {
        string name = baseName;
        for (int n = 2; Find(name) is not null; n++)
            name = $"{baseName}-{n}";
        return name;
    }
}
