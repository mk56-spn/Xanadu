using System;
using System.Collections.Generic;

namespace Xanadu.Particles;

/// <summary>The rules a particle library must keep to be saved, loaded or played.</summary>
public static class XpfValidation
{
    public const int MaxParticles = 1024;

    /// <summary>What is wrong with <paramref name="library"/>, or null when it is fine.</summary>
    public static string? Problem(XpfLibrary library)
    {
        if (library.Particles.Count > MaxParticles)
            return "Too many particles.";

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (XpfParticle particle in library.Particles)
        {
            string? problem = Problem(particle);
            if (problem is not null)
                return $"{particle.Name}: {problem}";
            if (!names.Add(particle.Name))
                return $"Two particles are called {particle.Name}.";
        }

        return null;
    }

    /// <summary>What is wrong with one effect, or null.</summary>
    public static string? Problem(XpfParticle particle)
    {
        if (string.IsNullOrWhiteSpace(particle.Name) || particle.Name.Length > XpfParticle.MaxNameLength)
            return "Bad name.";
        if (!Enum.IsDefined(particle.Texture) || !Enum.IsDefined(particle.Shape))
            return "Unknown texture or shape.";
        if (particle.Amount < 1 || particle.Amount > XpfParticle.MaxAmount)
            return "Amount out of range.";
        if (!Positive(particle.Lifetime) || !Positive(particle.Reach))
            return "Lifetime and reach must be above zero.";
        if (particle.ScaleCurve.Count > XpfParticle.MaxCurvePoints || particle.Ramp.Count > XpfParticle.MaxCurvePoints)
            return "Too many curve points.";

        float last = -1f;
        foreach (XpfCurvePoint point in particle.ScaleCurve)
        {
            if (!InUnit(point.Time) || point.Time < last || !float.IsFinite(point.Value))
                return "Scale curve out of order.";
            last = point.Time;
        }

        last = -1f;
        foreach (XpfColorStop stop in particle.Ramp)
        {
            if (!InUnit(stop.Time) || stop.Time < last)
                return "Colour ramp out of order.";
            last = stop.Time;
        }

        float[] values =
        {
            particle.Explosiveness, particle.Randomness, particle.BoxSize.X, particle.BoxSize.Y,
            particle.Direction, particle.Spread, particle.HeadedSpread, particle.SpeedMin, particle.SpeedMax,
            particle.Gravity.X, particle.Gravity.Y, particle.DampingMin, particle.DampingMax,
            particle.ScaleMin, particle.ScaleMax, particle.AngleMin, particle.AngleMax,
        };
        foreach (float value in values)
        {
            if (!float.IsFinite(value))
                return "A value is not a number.";
        }

        return null;
    }

    private static bool Positive(float value) => float.IsFinite(value) && value > 0f;

    private static bool InUnit(float value) => float.IsFinite(value) && value is >= 0f and <= 1f;
}
