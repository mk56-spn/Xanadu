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
public struct BenchView(Entity background, Entity title, Entity info, Entity player, Entity[] quarters, Entity[] notes) : IComponent
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

    /// <summary>A pool of vocal-note disks the system places on the song's beats as they scroll past.</summary>
    public Entity[] Notes = notes;
}
