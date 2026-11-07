using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>The settings of a flare the sidebar steps, in panel order.</summary>
public enum FlareSetting
{
    Width,
    Height,
    Axes,
    Thickness,
    Falloff,
    Core,
    Halo,
    Glow,
    Intensity,
    Heat,
}

/// <summary>
/// A lens flare in a level, drawn by a shader: a hot core, a soft halo round
/// it and a streak along one axis of its box, or one along each. Its box is
/// turned about its centre like grass's, so it is moved, resized and turned the
/// same way. It holds still; anything that moves or fades it over time does so
/// through groups, like any other item.
/// </summary>
public struct LevelFlare() : IComponent
{
    public const float DefaultWidth = 480f;
    public const float DefaultHeight = 240f;
    public const float MinSize = 16f;
    public const float MaxSize = 4_000f;

    /// <summary>The streak's thickness at the core, as a share of the box's shorter half.</summary>
    public const float DefaultThickness = 0.05f;
    public const float MinThickness = 0.005f;
    public const float MaxThickness = 0.5f;

    /// <summary>How quickly a streak fades towards its ends: 1 is a straight ramp, higher is shorter.</summary>
    public const float DefaultFalloff = 1.5f;
    public const float MinFalloff = 0.25f;
    public const float MaxFalloff = 8f;

    /// <summary>The core's radius, as a share of the box's shorter half.</summary>
    public const float DefaultCore = 0.15f;

    /// <summary>The halo's radius, as a share of the box's shorter half.</summary>
    public const float DefaultHalo = 0.9f;

    /// <summary>How bright the halo is, 0 to 1.</summary>
    public const float DefaultGlow = 0.35f;

    /// <summary>The whole flare's brightness; above 1 it saturates outwards.</summary>
    public const float DefaultIntensity = 1f;
    public const float MaxIntensity = 4f;

    /// <summary>How far the core and the streak's middle go towards white, 0 to 1.</summary>
    public const float DefaultHeat = 0.7f;

    public static readonly Color DefaultColor = new("#ff9a3c");

    /// <summary>The middle of the box and the core, in playfield units.</summary>
    public Vector2 Position;

    /// <summary>The box's width, which the first streak spans, in playfield units.</summary>
    public float Width = DefaultWidth;

    /// <summary>The box's height, which the second streak spans, in playfield units.</summary>
    public float Height = DefaultHeight;

    /// <summary>1 for a streak along the width only; 2 adds one along the height.</summary>
    public int Axes = 1;

    public float Thickness = DefaultThickness;
    public float Falloff = DefaultFalloff;
    public float Core = DefaultCore;
    public float Halo = DefaultHalo;
    public float Glow = DefaultGlow;
    public float Intensity = DefaultIntensity;
    public float Heat = DefaultHeat;

    public Color Color = DefaultColor;

    /// <summary>How far the flare is turned about its centre, clockwise, in degrees.</summary>
    public float RotationDegrees;

    public LevelFlare(Vector2 position) : this()
    {
        Position = position;
    }

    /// <summary>The flare's box before it is turned, in playfield units.</summary>
    public readonly Rect2 Bounds => new(Position.X - Width / 2, Position.Y - Height / 2, Width, Height);

    /// <summary>Gives the flare a new unturned box, centred where the box is.</summary>
    public void SetBounds(Rect2 box)
    {
        Width = Mathf.Clamp(box.Size.X, MinSize, MaxSize);
        Height = Mathf.Clamp(box.Size.Y, MinSize, MaxSize);
        Position = box.GetCenter();
    }

    /// <summary>One setting's value.</summary>
    public readonly float Get(FlareSetting setting) => setting switch
    {
        FlareSetting.Width => Width,
        FlareSetting.Height => Height,
        FlareSetting.Axes => Axes,
        FlareSetting.Thickness => Thickness,
        FlareSetting.Falloff => Falloff,
        FlareSetting.Core => Core,
        FlareSetting.Halo => Halo,
        FlareSetting.Glow => Glow,
        FlareSetting.Intensity => Intensity,
        _ => Heat,
    };

    /// <summary>Sets one setting, kept within its range.</summary>
    public void Set(FlareSetting setting, float value)
    {
        if (!float.IsFinite(value))
            value = 0f;

        switch (setting)
        {
            case FlareSetting.Width: Width = Mathf.Clamp(value, MinSize, MaxSize); break;
            case FlareSetting.Height: Height = Mathf.Clamp(value, MinSize, MaxSize); break;
            case FlareSetting.Axes: Axes = Mathf.Clamp(Mathf.RoundToInt(value), 1, 2); break;
            case FlareSetting.Thickness: Thickness = Mathf.Clamp(value, MinThickness, MaxThickness); break;
            case FlareSetting.Falloff: Falloff = Mathf.Clamp(value, MinFalloff, MaxFalloff); break;
            case FlareSetting.Core: Core = Mathf.Clamp(value, 0f, 1f); break;
            case FlareSetting.Halo: Halo = Mathf.Clamp(value, 0f, 1f); break;
            case FlareSetting.Glow: Glow = Mathf.Clamp(value, 0f, 1f); break;
            case FlareSetting.Intensity: Intensity = Mathf.Clamp(value, 0f, MaxIntensity); break;
            default: Heat = Mathf.Clamp(value, 0f, 1f); break;
        }
    }

    /// <summary>The same flare with every setting brought into range, for flares read from a file.</summary>
    public readonly LevelFlare Clamped()
    {
        LevelFlare flare = this;
        foreach (FlareSetting setting in System.Enum.GetValues<FlareSetting>())
            flare.Set(setting, Get(setting));
        flare.RotationDegrees = TurnedBox.Normalize(flare.RotationDegrees);
        return flare;
    }

    /// <summary>How much one press of a setting's - or + button changes it.</summary>
    public static float Step(FlareSetting setting) => setting switch
    {
        FlareSetting.Width or FlareSetting.Height => 20f,
        FlareSetting.Axes => 1f,
        FlareSetting.Thickness => 0.01f,
        FlareSetting.Falloff => 0.25f,
        FlareSetting.Intensity => 0.1f,
        FlareSetting.Heat => 0.1f,
        _ => 0.05f,
    };
}
