using Godot;
using Xanadu.Particles;

namespace Xanadu.Systems.Particles;

/// <summary>Turns an <see cref="XpfParticle"/> into the process material Godot's particles run.</summary>
public static class XpfMaterials
{
    /// <summary>A fresh material for <paramref name="particle"/>, aimed its own way and with its own box.</summary>
    public static ParticleProcessMaterial Build(XpfParticle particle)
    {
        var material = new ParticleProcessMaterial
        {
            ParticleFlagDisableZ = true,
            EmissionShape = particle.Shape == XpfShape.Box
                ? ParticleProcessMaterial.EmissionShapeEnum.Box
                : ParticleProcessMaterial.EmissionShapeEnum.Point,
            Direction = Direction(particle.Direction),
            Spread = particle.Spread,
            InitialVelocityMin = particle.SpeedMin,
            InitialVelocityMax = particle.SpeedMax,
            Gravity = new Vector3(particle.Gravity.X, particle.Gravity.Y, 0),
            DampingMin = particle.DampingMin,
            DampingMax = particle.DampingMax,
            ScaleMin = particle.ScaleMin,
            ScaleMax = particle.ScaleMax,
            AngleMin = particle.AngleMin,
            AngleMax = particle.AngleMax,
            Color = particle.Color,
        };

        SetBox(material, particle.BoxSize);
        if (particle.ScaleCurve.Count > 0)
            material.ScaleCurve = Curve(particle);
        if (particle.Ramp.Count > 0)
            material.ColorRamp = Ramp(particle);

        // Note glyphs are frames of one strip: a random offset picks each particle's cell.
        if (particle.Texture == XpfTexture.NoteGlyphs)
            material.AnimOffsetMax = 1f;

        return material;
    }

    /// <summary>
    /// Aims a playing material for one burst or stream: its box, a heading
    /// (on screen, zero keeps the effect's own direction) and the emitter's turn.
    /// </summary>
    public static void Aim(
        ParticleProcessMaterial material,
        XpfParticle particle,
        Vector2 area,
        float angle,
        Vector2 heading)
    {
        if (particle.Shape == XpfShape.Box)
            SetBox(material, area == Vector2.Zero ? particle.BoxSize : area);

        // The heading is on screen; the material is local to the turned item.
        if (heading != Vector2.Zero)
        {
            Vector2 local = heading.Normalized().Rotated(-angle);
            material.Direction = new Vector3(local.X, local.Y, 0);
            material.Spread = particle.HeadedSpread;
        }
        else
        {
            material.Direction = Direction(particle.Direction);
            material.Spread = particle.Spread;
        }

        if (particle.Upright)
        {
            float facing = -Mathf.RadToDeg(angle);
            material.AngleMin = facing + particle.AngleMin;
            material.AngleMax = facing + particle.AngleMax;
        }
    }

    private static Vector3 Direction(float degrees)
    {
        Vector2 way = Vector2.FromAngle(Mathf.DegToRad(degrees));
        return new Vector3(way.X, way.Y, 0);
    }

    private static void SetBox(ParticleProcessMaterial material, Vector2 size) =>
        material.EmissionBoxExtents = new Vector3(size.X / 2, size.Y / 2, 0);

    private static CurveTexture Curve(XpfParticle particle)
    {
        var curve = new Curve();
        foreach (XpfCurvePoint point in particle.ScaleCurve)
            curve.AddPoint(new Vector2(point.Time, point.Value));
        return new CurveTexture { Curve = curve };
    }

    private static GradientTexture1D Ramp(XpfParticle particle)
    {
        var gradient = new Gradient();
        var offsets = new float[particle.Ramp.Count];
        var colors = new Color[particle.Ramp.Count];
        for (int i = 0; i < particle.Ramp.Count; i++)
        {
            offsets[i] = particle.Ramp[i].Time;
            colors[i] = particle.Ramp[i].Color;
        }

        gradient.Offsets = offsets;
        gradient.Colors = colors;
        return new GradientTexture1D { Gradient = gradient };
    }
}
