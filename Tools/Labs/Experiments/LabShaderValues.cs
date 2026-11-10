using System;
using System.Linq;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Values the lab can always hand a slot shader, by uniform name. Each is set
/// only when the shader declares a uniform of that name, so a shader asks for
/// one just by writing it. Shared by the shader playground and editor.
/// </summary>
internal static class LabShaderValues
{
    /// <summary>What the values are made from this frame.</summary>
    public readonly record struct Provided(Vector2 Centre, float Time, Vector2 Mouse, Vector2 Resolution);

    public static readonly (string Name, Func<Provided, Vector4> Value)[] All =
    [
        ("centre", p => new Vector4(p.Centre.X, p.Centre.Y, 0, 0)),
        ("time", p => new Vector4(p.Time, 0, 0, 0)),
        ("mouse", p => new Vector4(p.Mouse.X, p.Mouse.Y, 0, 0)),
        ("resolution", p => new Vector4(p.Resolution.X, p.Resolution.Y, 0, 0)),
    ];

    /// <summary>The lines a panel shows to say which values exist.</summary>
    public static readonly string[] Notes =
    [
        "Declare any of these and the lab fills it every frame:",
        "  float2 centre: the pointer in i.centred's space",
        "  float time: seconds since the lab started",
        "  float2 mouse: the pointer, 0..1 on the stage",
        "  float2 resolution: the stage's size in pixels",
    ];

    /// <summary>Which of <see cref="All"/> a compiled shader declares, by index.</summary>
    public static int[] Wanted(string[] declared) =>
        Enumerable.Range(0, All.Length).Where(index => Array.IndexOf(declared, All[index].Name) >= 0).ToArray();

    /// <summary>Sets the wanted values on a material for a stage drawn in <paramref name="area"/>.</summary>
    public static void Apply(CanvasMaterial material, int[] wanted, Rect2 area, Vector2 pointer, float time)
    {
        Vector2 mouse = (pointer - area.Position) / area.Size;
        mouse = new Vector2(Mathf.Clamp(mouse.X, 0, 1), Mathf.Clamp(mouse.Y, 0, 1));
        var provided = new Provided((mouse - new Vector2(0.5f, 0.5f)) * area.Size, time, mouse, area.Size);
        foreach (int index in wanted)
            material.Set(All[index].Name, All[index].Value(provided));
    }

    /// <summary>" · lab sets centre, time", or empty.</summary>
    public static string Describe(int[] wanted) =>
        wanted.Length == 0 ? "" : " · lab sets " + string.Join(", ", wanted.Select(index => All[index].Name));
}
