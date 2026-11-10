using System;
using System.Collections.Generic;

namespace Xanadu.Particles;

/// <summary>A number of an effect the particle editor sets with a slider.</summary>
public enum XpfField
{
    Amount,
    Lifetime,
    Explosiveness,
    Randomness,
    BoxWidth,
    BoxHeight,
    Direction,
    Spread,
    SpeedMin,
    SpeedMax,
    GravityX,
    GravityY,
    DampingMin,
    DampingMax,
    ScaleMin,
    ScaleMax,
    AngleMin,
    AngleMax,
    Reach,
}

/// <summary>A size over life the editor offers; the file keeps any curve.</summary>
public enum XpfScalePreset
{
    Flat,
    Shrink,
    Grow,
    Pop,
}

/// <summary>A fade over life the editor offers; the file keeps any ramp.</summary>
public enum XpfFadePreset
{
    None,
    Out,
    InOut,
}

/// <summary>How a slider shows and limits one <see cref="XpfField"/>.</summary>
public readonly record struct XpfFieldRange(string Label, float Min, float Max, string Format, string Tooltip);

/// <summary>The edits the particle editor makes to effects and the library.</summary>
public static class XpfEditing
{
    public static readonly XpfField[] Fields = Enum.GetValues<XpfField>();

    public static XpfFieldRange Range(XpfField field) => field switch
    {
        XpfField.Amount => new("AMOUNT", 1, 300, "0", "Particles alive at once; a burst throws this many"),
        XpfField.Lifetime => new("LIFE", 0.05f, 4f, "0.00", "How long each particle lives, in seconds"),
        XpfField.Explosiveness => new("BURST", 0, 1, "0.00", "0 spreads births over the life, 1 throws them all at once"),
        XpfField.Randomness => new("JITTER", 0, 1, "0.00", "How much each particle's life wanders"),
        XpfField.BoxWidth => new("BOX W", 0, 400, "0", "Width of the box particles start in, when nothing gives one"),
        XpfField.BoxHeight => new("BOX H", 0, 400, "0", "Height of the box particles start in"),
        XpfField.Direction => new("AIM", -180, 180, "0", "The way particles are thrown, in degrees: 0 right, 90 down"),
        XpfField.Spread => new("SPREAD", 0, 180, "0", "Half the cone they are thrown in; 180 is every way"),
        XpfField.SpeedMin => new("SPEED LO", 0, 1500, "0", "Slowest throw, in pixels a second"),
        XpfField.SpeedMax => new("SPEED HI", 0, 1500, "0", "Fastest throw, in pixels a second"),
        XpfField.GravityX => new("PULL X", -2000, 2000, "0", "Sideways pull, in pixels a second squared"),
        XpfField.GravityY => new("PULL Y", -2000, 2000, "0", "Downward pull; negative rises"),
        XpfField.DampingMin => new("BRAKE LO", 0, 1000, "0", "Least braking"),
        XpfField.DampingMax => new("BRAKE HI", 0, 1000, "0", "Most braking"),
        XpfField.ScaleMin => new("SIZE LO", 0, 8, "0.00", "Smallest particle"),
        XpfField.ScaleMax => new("SIZE HI", 0, 8, "0.00", "Largest particle"),
        XpfField.AngleMin => new("TURN LO", -180, 180, "0", "Least turn at birth, in degrees"),
        XpfField.AngleMax => new("TURN HI", -180, 180, "0", "Most turn at birth, in degrees"),
        XpfField.Reach => new("REACH", 50, 2000, "0", "How far from the emitter particles still draw"),
        _ => throw new ArgumentOutOfRangeException(nameof(field)),
    };

    public static float Get(XpfParticle particle, XpfField field) => field switch
    {
        XpfField.Amount => particle.Amount,
        XpfField.Lifetime => particle.Lifetime,
        XpfField.Explosiveness => particle.Explosiveness,
        XpfField.Randomness => particle.Randomness,
        XpfField.BoxWidth => particle.BoxSize.X,
        XpfField.BoxHeight => particle.BoxSize.Y,
        XpfField.Direction => particle.Direction,
        XpfField.Spread => particle.Spread,
        XpfField.SpeedMin => particle.SpeedMin,
        XpfField.SpeedMax => particle.SpeedMax,
        XpfField.GravityX => particle.Gravity.X,
        XpfField.GravityY => particle.Gravity.Y,
        XpfField.DampingMin => particle.DampingMin,
        XpfField.DampingMax => particle.DampingMax,
        XpfField.ScaleMin => particle.ScaleMin,
        XpfField.ScaleMax => particle.ScaleMax,
        XpfField.AngleMin => particle.AngleMin,
        XpfField.AngleMax => particle.AngleMax,
        XpfField.Reach => particle.Reach,
        _ => 0f,
    };

    /// <summary>Sets a field, held inside its slider's range; a low end pushes its high end up with it and back.</summary>
    public static void Set(XpfParticle particle, XpfField field, float value)
    {
        XpfFieldRange range = Range(field);
        if (!float.IsFinite(value))
            return;
        value = Math.Clamp(value, range.Min, range.Max);
        switch (field)
        {
            case XpfField.Amount: particle.Amount = (int)MathF.Round(value); break;
            case XpfField.Lifetime: particle.Lifetime = value; break;
            case XpfField.Explosiveness: particle.Explosiveness = value; break;
            case XpfField.Randomness: particle.Randomness = value; break;
            case XpfField.BoxWidth: particle.BoxSize.X = value; break;
            case XpfField.BoxHeight: particle.BoxSize.Y = value; break;
            case XpfField.Direction: particle.Direction = value; break;
            case XpfField.Spread: particle.Spread = value; break;
            case XpfField.SpeedMin: particle.SpeedMin = value; particle.SpeedMax = Math.Max(particle.SpeedMax, value); break;
            case XpfField.SpeedMax: particle.SpeedMax = value; particle.SpeedMin = Math.Min(particle.SpeedMin, value); break;
            case XpfField.GravityX: particle.Gravity.X = value; break;
            case XpfField.GravityY: particle.Gravity.Y = value; break;
            case XpfField.DampingMin: particle.DampingMin = value; particle.DampingMax = Math.Max(particle.DampingMax, value); break;
            case XpfField.DampingMax: particle.DampingMax = value; particle.DampingMin = Math.Min(particle.DampingMin, value); break;
            case XpfField.ScaleMin: particle.ScaleMin = value; particle.ScaleMax = Math.Max(particle.ScaleMax, value); break;
            case XpfField.ScaleMax: particle.ScaleMax = value; particle.ScaleMin = Math.Min(particle.ScaleMin, value); break;
            case XpfField.AngleMin: particle.AngleMin = value; particle.AngleMax = Math.Max(particle.AngleMax, value); break;
            case XpfField.AngleMax: particle.AngleMax = value; particle.AngleMin = Math.Min(particle.AngleMin, value); break;
            case XpfField.Reach: particle.Reach = value; break;
        }
    }

    public static XpfScalePreset ScalePreset(XpfParticle particle) => particle.ScaleCurve switch
    {
        [] => XpfScalePreset.Flat,
        [{ Value: 1f }, { Value: 0f }] => XpfScalePreset.Shrink,
        [{ Value: 0f }, { Value: 1f }] => XpfScalePreset.Grow,
        [{ Value: 0f }, { Value: 1f }, { Value: 0f }] => XpfScalePreset.Pop,
        _ => XpfScalePreset.Flat,
    };

    public static void SetScalePreset(XpfParticle particle, XpfScalePreset preset) =>
        particle.ScaleCurve = preset switch
        {
            XpfScalePreset.Shrink => new() { new(0f, 1f), new(1f, 0f) },
            XpfScalePreset.Grow => new() { new(0f, 0f), new(1f, 1f) },
            XpfScalePreset.Pop => new() { new(0f, 0f), new(0.2f, 1f), new(1f, 0f) },
            _ => new List<XpfCurvePoint>(),
        };

    public static XpfFadePreset FadePreset(XpfParticle particle) => particle.Ramp switch
    {
        [] => XpfFadePreset.None,
        [_, _] => XpfFadePreset.Out,
        [_, _, _] => XpfFadePreset.InOut,
        _ => XpfFadePreset.None,
    };

    /// <summary>Sets the fade; the ramp is white, so the effect's own colour shows through it.</summary>
    public static void SetFadePreset(XpfParticle particle, XpfFadePreset preset)
    {
        var clear = new Color(1, 1, 1, 0);
        particle.Ramp = preset switch
        {
            XpfFadePreset.Out => new() { new(0f, Colors.White), new(1f, clear) },
            XpfFadePreset.InOut => new() { new(0f, clear), new(0.2f, Colors.White), new(1f, clear) },
            _ => new List<XpfColorStop>(),
        };
    }

    /// <summary>The next value of an enum, wrapping round.</summary>
    public static T Next<T>(T value) where T : struct, Enum
    {
        T[] values = Enum.GetValues<T>();
        return values[(Array.IndexOf(values, value) + 1) % values.Length];
    }

    /// <summary>Adds a plain effect and returns its name.</summary>
    public static string Add(XpfLibrary library, string baseName = "particle")
    {
        var particle = new XpfParticle { Name = library.UniqueName(baseName) };
        SetFadePreset(particle, XpfFadePreset.Out);
        library.Particles.Add(particle);
        return particle.Name;
    }

    /// <summary>Adds a copy of <paramref name="name"/> under a new name and returns it; null when there is no such effect.</summary>
    public static string? Copy(XpfLibrary library, string name)
    {
        XpfParticle? source = library.Find(name);
        if (source is null)
            return null;

        XpfParticle copy = source.Clone();
        copy.Name = library.UniqueName(name);
        library.Particles.Add(copy);
        return copy.Name;
    }

    /// <summary>Renames an effect; refused (false) for a blank, too long or taken name.</summary>
    public static bool Rename(XpfLibrary library, string name, string newName)
    {
        newName = newName.Trim();
        XpfParticle? particle = library.Find(name);
        if (particle is null || newName.Length == 0 || newName.Length > XpfParticle.MaxNameLength)
            return false;
        if (newName == name)
            return true;
        if (library.Find(newName) is not null)
            return false;

        particle.Name = newName;
        return true;
    }

    /// <summary>The name after (or before, with <paramref name="step"/> -1) <paramref name="name"/> in the library, wrapping round.</summary>
    public static string? Step(XpfLibrary library, string? name, int step)
    {
        int count = library.Particles.Count;
        if (count == 0)
            return null;

        int index = name is null ? -1 : library.Particles.FindIndex(particle => particle.Name == name);
        if (index < 0)
            return library.Particles[step >= 0 ? 0 : count - 1].Name;
        return library.Particles[((index + step) % count + count) % count].Name;
    }
}
