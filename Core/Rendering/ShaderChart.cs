using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Xanadu.Rendering;

/// <summary>
/// One note of the chart as shaders read it (<c>notes.</c> in <c>xanadu.play</c>,
/// xanadu.stage's <c>ChartNote</c>): where and when it is, and how it was
/// played. Positions are playfield units; times are song seconds.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ChartNote
{
    /// <summary>Where it is placed (xy), before any group moves it.</summary>
    public Vector4 Place;

    /// <summary>Its colour.</summary>
    public Vector4 Colour;

    /// <summary>When it is due (x), its kind (y, as the game's <c>NoteKind</c>), its action key (z), its index (w).</summary>
    public Vector4 Timing;

    /// <summary>
    /// When it was judged (x, <see cref="ShaderKeys.Never"/> while it waits), the
    /// judgement (y, as the game numbers them), how early (−) or late (+) in
    /// milliseconds (z), and its state (w: <see cref="ShaderChart.Waiting"/>,
    /// <see cref="ShaderChart.Hit"/>, <see cref="ShaderChart.Missed"/>).
    /// </summary>
    public Vector4 Result;

    /// <summary>Where it was when judged, groups' moves included (xy).</summary>
    public Vector4 Judged;

    public const int Size = 80;
}

/// <summary>
/// The whole chart of the run being played, for shaders: every note, from the
/// start, with each one's result written as it is judged. The renderer keeps a
/// copy on the GPU and uploads it again whenever <see cref="Version"/> moves.
/// The play scene loads it; the judgement points write results; the tools'
/// stand-ins fake one.
/// </summary>
public static class ShaderChart
{
    public const float Waiting = 0, Hit = 1, Missed = 2;

    private static ChartNote[] _notes = [];

    /// <summary>The notes, in chart order (by time).</summary>
    public static ReadOnlySpan<ChartNote> Notes => _notes;

    public static int Count => _notes.Length;

    /// <summary>Moves on every change, so the renderer knows to upload.</summary>
    public static int Version { get; private set; }

    /// <summary>A new chart: every note waiting.</summary>
    public static void Load(ReadOnlySpan<ChartNote> notes)
    {
        _notes = notes.ToArray();
        for (int i = 0; i < _notes.Length; i++)
        {
            _notes[i].Timing.W = i;
            _notes[i].Result = new Vector4(ShaderKeys.Never, 0, 0, Waiting);
            _notes[i].Judged = _notes[i].Place;
        }

        Version++;
    }

    /// <summary>Note <paramref name="index"/> was judged at <paramref name="seconds"/>, where it stood then.</summary>
    public static void Judge(int index, float seconds, float judgement, float errorMs, float state, Vector2 place)
    {
        if (index < 0 || index >= _notes.Length)
            return;

        _notes[index].Result = new Vector4(seconds, judgement, errorMs, state);
        _notes[index].Judged = new Vector4(place.X, place.Y, 0, 0);
        Version++;
    }

    /// <summary>No chart.</summary>
    public static void Clear()
    {
        _notes = [];
        Version++;
    }
}
