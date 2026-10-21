using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;

namespace Writer.Systems.Groups;

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
