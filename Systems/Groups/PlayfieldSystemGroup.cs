using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Settings;
using Writer.Systems.Input;
using Writer.Settings;
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
        KeybindSettings keybinds,
        IKeyboardState keyboard,
        OffsetSettings offsets,
        IPointerInputSource? pointer = null,
        HudLayoutSettings? hudLayout = null,
        Rid hudOverlay = default) : base("Playfield")
    {
        Add(new PlayfieldPlaybackSystem(audio));
        Add(new PlaySessionSystem(audio));
        Add(new PlayerMovementSystem(entities, keybinds, keyboard, offsets));
        Add(new PlayerArenaSystem());
        Add(new PlayKeyTrailSystem(entities, keybinds, keyboard));
        Add(new PlayNoteInputSystem(entities, keybinds, keyboard, offsets));
        Add(new HitTrailSystem());
        Add(new DashTrailSystem());
        Add(new HudLayoutSystem(
            canvas,
            pointer ?? new NullPointerInputSource(),
            keyboard,
            hudLayout ?? new HudLayoutSettings(persist: false),
            hudOverlay));
        Add(new ContactCounterSystem());
        Add(new PlayScoreSystem());
        Add(new PlayfieldDrawSystem(entities, canvas, playfieldCanvas));
    }
}
