using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using System;
using Godot;
using Xanadu.Animation;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Characters;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Particles;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Replays;

namespace Xanadu.Systems.Groups;

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
        Rid hudOverlay = default,
        ICharacterRigFiles? rigFiles = null,
        Func<IAttachmentVisuals>? rigVisuals = null,
        IParticleEmitter? particles = null,
        ReplayKeyboard? replayKeyboard = null,
        Rid hitboxCanvas = default,
        IOverlayToggleSource? hitboxToggle = null) : base("Playfield")
    {
        Add(new PlayfieldPlaybackSystem(audio));
        Add(new PlaySessionSystem(audio));
        Add(new GroupModulationSystem(entities));
        if (replayKeyboard is not null)
        {
            // Before anything that reads the keys, so a replayed press is seen this frame.
            Add(new ReplayRecordSystem(keybinds, replayKeyboard.Real));
            Add(new ReplayPlaybackSystem(keybinds, replayKeyboard));
        }

        Add(new PlayerMovementSystem(entities, keybinds, keyboard, offsets));
        Add(new PlayerArenaSystem());
        Add(new PlayKeyTrailSystem(entities, keybinds, keyboard));
        Add(new PlayNoteInputSystem(entities, keybinds, keyboard, offsets));
        Add(new HitTrailSystem());
        Add(new DashTrailSystem());
        if (particles is not null)
            Add(new ParticleBurstSystem(particles));
        Add(new HudLayoutSystem(
            canvas,
            pointer ?? new NullPointerInputSource(),
            keyboard,
            hudLayout ?? new HudLayoutSettings(persist: false),
            hudOverlay));
        Add(new ContactCounterSystem());
        Add(new PlayScoreSystem());
        if (rigFiles is not null && rigVisuals is not null)
        {
            Add(new CharacterRigLoadSystem(rigFiles));
            Add(new CharacterMotionSystem());
            Add(new CharacterRigAnimateSystem());
            Add(new CharacterRigDrawSystem(rigVisuals));
        }

        Add(new PlayfieldDrawSystem(entities, canvas, playfieldCanvas));
        if (hitboxToggle is not null)
        {
            Add(new PlayfieldHitboxOverlaySystem(
                entities,
                canvas,
                hitboxCanvas,
                hitboxToggle));
        }
    }
}
