using System;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Groups;

/// <summary>Advances track playback and draws its timeline.</summary>
public sealed class AudioSystemGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _timeline;

    public AudioSystemGroup(
        ITrackAudio audio,
        ITrackClock clock,
        IRectCanvas canvas,
        Rid parentCanvas) : base("Audio")
    {
        _timeline = new CanvasItemHandle(parentCanvas, 900);
        Add(new TrackPlaybackSystem(audio, clock));
        Add(new TimelineDrawSystem(canvas, _timeline.Rid));
    }

    public void Dispose() => _timeline.Dispose();
}
