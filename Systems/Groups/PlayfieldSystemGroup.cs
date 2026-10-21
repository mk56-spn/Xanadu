using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Input;
using Writer.Systems.Playfields;

namespace Writer.Systems.Groups;

/// <summary>Shared playback and playfield rendering for editor and play scenes.</summary>
public sealed class PlayfieldSystemGroup : SystemGroup
{
    public PlayfieldSystemGroup(
        EntityStore entities,
        IRectCanvas canvas,
        Rid playfieldCanvas,
        ITrackAudio audio,
        IPointerInputSource pointer) : base("Playfield")
    {
        Add(new PlayfieldPlaybackSystem(audio));
        Add(new PlaySessionSystem());
        Add(new PlayNoteInputSystem(entities, pointer));
        Add(new PlayScoreSystem());
        Add(new PlayfieldDrawSystem(entities, canvas, playfieldCanvas));
    }
}
