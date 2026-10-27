using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Groups;

/// <summary>Advances track playback and draws its timeline.</summary>
public sealed class AudioSystemGroup : SystemGroup
{
    public AudioSystemGroup(ITrackAudio audio, ITrackClock clock, IRectCanvas canvas, Rid timeline)
        : base("Audio")
    {
        Add(new TrackPlaybackSystem(audio, clock));
        Add(new TimelineDrawSystem(canvas, timeline));
    }
}
