using Friflo.Engine.ECS;

namespace Xanadu.Model.Ecs.Components;

/// <summary>The bench's buttons: step to the previous or next playable song.</summary>
public enum BenchAction
{
    Previous,
    Next,
}

/// <summary>
/// The gameplay bench: a feel test reached from the main menu. It plays a song
/// from the library and pulses the background on that song's beats, behind a
/// surface and the player box. The system owns the audio and writes through
/// these handles.
/// </summary>
public struct BenchView(
    Entity background,
    Entity title,
    Entity info,
    Entity player,
    Entity[] quarters,
    Entity[] notes,
    Entity surface = default,
    Entity[]? curves = null) : IComponent
{
    /// <summary>The full-stage backdrop that pulses on the beat.</summary>
    public Entity Background = background;

    public Entity Title = title;

    /// <summary>The song's tempo and the beat it is on.</summary>
    public Entity Info = info;

    /// <summary>The player box: it runs at a fixed speed (the world scrolls past) and its height is set by the vocal notes.</summary>
    public Entity Player = player;

    /// <summary>The four action keys as quarter disks around the player, lit while held.</summary>
    public Entity[] Quarters = quarters;

    /// <summary>A pool of note disks: the song's beats, or a map's notes when it has one.</summary>
    public Entity[] Notes = notes;

    /// <summary>The flat surface under the beat run; a map has no floor yet, so it is put away there.</summary>
    public Entity Surface = surface;

    /// <summary>A pool of open lines the system draws a map's curve notes with.</summary>
    public Entity[] Curves = curves ?? [];
}

/// <summary>
/// The bench's body during a map run, on the player box: where it is on the
/// playfield, where the curve the song is on wants it, and the song time. The
/// bench writes it every frame so a watcher (a test, a debug view) can see how
/// well the body follows.
/// </summary>
public struct BenchBody : IComponent
{
    public Vector2 Position;

    /// <summary>The point on the curve nearest the body, the one the line pulls toward; only meaningful while <see cref="OnCurve"/>.</summary>
    public Vector2 Target;

    public bool OnCurve;

    /// <summary>The song position this was written at, ms; NaN while the song isn't playing.</summary>
    public double SongMs;
}
