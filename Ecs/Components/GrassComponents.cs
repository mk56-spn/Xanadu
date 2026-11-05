using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>The settings of a grass patch the sidebar steps, in panel order.</summary>
public enum GrassSetting
{
    Width,
    Length,
    Density,
    Sway,
    Variation,
}

/// <summary>
/// A strip of grass in a level: blades stand along a base line and grow up
/// from it, swaying. It has no timing and no collision; groups fade and move
/// it like any other item.
/// </summary>
public struct GrassPatch() : IComponent
{
    public const float DefaultWidth = 240f;
    public const float MinWidth = 16f;
    public const float MaxWidth = 4_000f;

    public const float DefaultLength = 48f;
    public const float MinLength = 8f;
    public const float MaxLength = 400f;

    /// <summary>Blades per 100 playfield units of width.</summary>
    public const float DefaultDensity = 40f;
    public const float MinDensity = 5f;
    public const float MaxDensity = 200f;

    /// <summary>How far a blade leans at the most, in degrees.</summary>
    public const float DefaultSway = 12f;
    public const float MaxSway = 60f;

    /// <summary>How far each blade's colour may stray from the patch's, 0 to 1.</summary>
    public const float DefaultVariation = 0.3f;

    /// <summary>The most blades one patch draws, whatever its width and density.</summary>
    public const int MaxBlades = 4_000;

    public static readonly Color DefaultColor = new("#4ade80");

    /// <summary>The middle of the base line, in playfield units; the blades grow up from it.</summary>
    public Vector2 Position;

    /// <summary>How long the base line is, in playfield units.</summary>
    public float Width = DefaultWidth;

    /// <summary>The tallest blade's length, in playfield units.</summary>
    public float Length = DefaultLength;

    /// <summary>Blades per 100 playfield units of width.</summary>
    public float Density = DefaultDensity;

    /// <summary>How far a blade leans at the most, in degrees.</summary>
    public float Sway = DefaultSway;

    public Color Color = DefaultColor;

    /// <summary>How far each blade's colour may stray from <see cref="Color"/>, 0 to 1.</summary>
    public float Variation = DefaultVariation;

    /// <summary>
    /// Picks where each blade stands and how it looks. It is kept with the
    /// patch, so moving the patch, by hand or by a group, moves the same blades.
    /// </summary>
    public uint Seed;

    public GrassPatch(Vector2 position, uint seed) : this()
    {
        Position = position;
        Seed = seed;
    }

    /// <summary>A seed for a newly placed patch.</summary>
    public static uint NewSeed() => (uint)System.Random.Shared.Next() ^ ((uint)System.Random.Shared.Next() << 1);

    /// <summary>How many blades the patch draws.</summary>
    public readonly int BladeCount => Mathf.Clamp(Mathf.RoundToInt(Width * Density / 100f), 1, MaxBlades);

    /// <summary>The box the blades fill when they stand straight, in playfield units.</summary>
    public readonly Rect2 Bounds => new(Position.X - Width / 2, Position.Y - Length, Width, Length);

    /// <summary>One setting's value.</summary>
    public readonly float Get(GrassSetting setting) => setting switch
    {
        GrassSetting.Width => Width,
        GrassSetting.Length => Length,
        GrassSetting.Density => Density,
        GrassSetting.Sway => Sway,
        _ => Variation,
    };

    /// <summary>Sets one setting, kept within its range.</summary>
    public void Set(GrassSetting setting, float value)
    {
        switch (setting)
        {
            case GrassSetting.Width: Width = Mathf.Clamp(value, MinWidth, MaxWidth); break;
            case GrassSetting.Length: Length = Mathf.Clamp(value, MinLength, MaxLength); break;
            case GrassSetting.Density: Density = Mathf.Clamp(value, MinDensity, MaxDensity); break;
            case GrassSetting.Sway: Sway = Mathf.Clamp(value, 0f, MaxSway); break;
            default: Variation = Mathf.Clamp(value, 0f, 1f); break;
        }
    }

    /// <summary>The same patch with every setting brought into range, for patches read from a file.</summary>
    public readonly GrassPatch Clamped()
    {
        GrassPatch patch = this;
        foreach (GrassSetting setting in System.Enum.GetValues<GrassSetting>())
            patch.Set(setting, float.IsFinite(Get(setting)) ? Get(setting) : 0f);
        return patch;
    }

    /// <summary>How much one press of a setting's - or + button changes it.</summary>
    public static float Step(GrassSetting setting) => setting switch
    {
        GrassSetting.Width => 16f,
        GrassSetting.Length => 8f,
        GrassSetting.Density => 5f,
        GrassSetting.Sway => 2f,
        _ => 0.05f,
    };
}
