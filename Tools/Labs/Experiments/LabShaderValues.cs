using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// Values the lab can always hand a slot shader, by name. A shader asks for one
/// just by using it: <see cref="Declare"/> adds the uniform for any it uses
/// without declaring, and the lab sets it every frame. Shared by the shader
/// playground and editor.
/// </summary>
internal static partial class LabShaderValues
{
    /// <summary>What the values are made from this frame.</summary>
    public readonly record struct Provided(Vector2 MousePos, float Time, Vector2 Mouse, Vector2 Resolution);

    public static readonly (string Name, string Type, Func<Provided, Vector4> Value)[] All =
    [
        ("mousePos", "float2", p => new Vector4(p.MousePos.X, p.MousePos.Y, 0, 0)),
        ("time", "float", p => new Vector4(p.Time, 0, 0, 0)),
        ("mouse", "float2", p => new Vector4(p.Mouse.X, p.Mouse.Y, 0, 0)),
        ("resolution", "float2", p => new Vector4(p.Resolution.X, p.Resolution.Y, 0, 0)),
    ];

    /// <summary>The lines a panel shows to say which values exist.</summary>
    public static readonly string[] Notes =
    [
        "Use any of these and the lab fills it every frame:",
        "  mousePos: the pointer in i.centred's space",
        "  time: seconds since the lab started",
        "  mouse: the pointer, 0..1 on the stage",
        "  resolution: the stage's size in pixels",
    ];

    /// <summary>
    /// The source with a uniform added for each provided value it uses but
    /// doesn't declare. They go after the last line (Slang doesn't mind where a
    /// global is declared), so error line numbers stay the author's.
    /// </summary>
    public static string Declare(string source)
    {
        string code = Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline), @"//[^\n]*", "");
        var added = new StringBuilder();
        foreach ((string name, string type, _) in All)
        {
            // Used as a name of its own (not i.time), and not declared anywhere already.
            bool used = Regex.IsMatch(code, $@"(?<![\w.]){name}\b");
            bool declared = Regex.IsMatch(code, $@"\b\w+\s+{name}\s*[;=,)\[]");
            if (used && !declared)
                added.Append(' ').Append($"uniform {type} {name};");
        }

        return added.Length == 0 ? source : source.TrimEnd('\n') + added + "\n";
    }

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

    /// <summary>" · lab sets mousePos, time", or empty.</summary>
    public static string Describe(int[] wanted) =>
        wanted.Length == 0 ? "" : " · lab sets " + string.Join(", ", wanted.Select(index => All[index].Name));
}
