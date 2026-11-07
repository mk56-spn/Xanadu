using Godot;
using Xanadu.Ecs;

namespace Xanadu.Levels;

/// <summary>Decorations read from a document: their settings come back into range, since a file can hold anything.</summary>
public static partial class LevelDocumentMapper
{
    /// <summary>The patch a document describes, its settings brought into range.</summary>
    public static GrassPatch ToPatch(GrassDocument grass) => new GrassPatch(new Vector2(grass.X, grass.Y), grass.Seed)
    {
        Width = grass.Width,
        Length = grass.Length,
        Density = grass.Density,
        Sway = grass.Sway,
        Color = new Color(grass.Color),
        Variation = grass.Variation,
        RotationDegrees = grass.Rotation,
    }.Clamped();

    /// <summary>The flare a document describes, its settings brought into range.</summary>
    public static LevelFlare ToFlare(FlareDocument flare) => new LevelFlare(new Vector2(flare.X, flare.Y))
    {
        Width = flare.Width,
        Height = flare.Height,
        Axes = flare.Axes,
        Thickness = flare.Thickness,
        Falloff = flare.Falloff,
        Core = flare.Core,
        Halo = flare.Halo,
        Glow = flare.Glow,
        Intensity = flare.Intensity,
        Heat = flare.Heat,
        Color = new Color(flare.Color),
        RotationDegrees = flare.Rotation,
    }.Clamped();
}
