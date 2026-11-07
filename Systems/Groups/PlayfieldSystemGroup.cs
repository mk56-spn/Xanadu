using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using System;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Characters;
using Xanadu.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Particles;
using Xanadu.Systems.Playfields;
using Xanadu.Systems.Replays;

namespace Xanadu.Systems.Groups;

/// <summary>
/// Shared playback and playfield rendering for editor and play scenes. Owns the
/// playfield's layer tree: the field itself, the character art drawn over it,
/// particle bursts, the F6 hitbox outlines, and the HUD edit overlay.
/// </summary>
public sealed class PlayfieldSystemGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _playfield;
    private readonly CanvasItemHandle _ghosts;
    private readonly CanvasItemHandle _characters;
    private readonly CanvasItemHandle _particleCanvas;
    private readonly CanvasItemHandle _hitboxes;
    private readonly CanvasItemHandle _hudOverlay;
    private readonly GodotParticleEmitter _particles;

    public PlayfieldSystemGroup(
        EntityStore entities,
        IRectCanvas canvas,
        Rid parentCanvas,
        ITrackAudio audio,
        KeybindSettings keybinds,
        IKeyboardState keyboard,
        OffsetSettings offsets,
        IPointerInputSource? pointer = null,
        HudLayoutSettings? hudLayout = null,
        ICharacterRigFiles? rigFiles = null,
        ReplayKeyboard? replayKeyboard = null,
        IOverlayToggleSource? hitboxToggle = null,
        MapOffsetSettings? mapOffsets = null,
        Xanadu.Systems.Shaders.IPassSurfaces? passes = null,
        Func<Rect2, Rect2>? toPixels = null,
        IOverlayToggleSource? divaToggle = null) : base("Playfield")
    {
        _playfield = new CanvasItemHandle(parentCanvas, 800);

        // The fading copies a character leaves on a hit: before the characters,
        // so the live art always draws over them.
        _ghosts = new CanvasItemHandle(_playfield.Rid, 0);

        // Character rig art: a child of the playfield, so it draws over the field.
        _characters = new CanvasItemHandle(_playfield.Rid, 0);

        // Particle bursts: after the characters, so they draw over the rig.
        _particleCanvas = new CanvasItemHandle(_playfield.Rid, 0);
        _particles = new GodotParticleEmitter(_particleCanvas.Rid);

        // The F6 hitbox outlines: after the art, so the rig cannot hide them,
        // but still inside the playfield's layer so the HUD stays on top.
        _hitboxes = new CanvasItemHandle(_playfield.Rid, 0, visible: false);

        _hudOverlay = new CanvasItemHandle(parentCanvas, 950);

        Add(new PlayfieldPlaybackSystem(audio));
        Add(new PlaySessionSystem(audio));
        Add(new PlaySkipSystem());
        Add(new GroupModulationSystem(entities));
        if (mapOffsets is not null)
            Add(new MapOffsetSystem(replayKeyboard?.Real ?? keyboard, mapOffsets, offsets));

        if (replayKeyboard is not null)
        {
            // Before anything that reads the keys, so a replayed press is seen this frame.
            Add(new ReplayRecordSystem(keybinds, replayKeyboard.Real, offsets));
            Add(new ReplayPlaybackSystem(keybinds, replayKeyboard));
        }

        Add(new PlayerMovementSystem(entities, keybinds, keyboard, offsets));
        Add(new PlayerArenaSystem());
        Add(new SpikeContactSystem(entities));
        Add(new PlayKeyTrailSystem(entities, keybinds, keyboard));
        Add(new PlayKeyTrailHudSystem(entities));
        Add(new PlayNoteInputSystem(entities, keybinds, keyboard, offsets));
        Add(new RailHoldSystem(keyboard, keybinds));
        Add(new ContactBarrierSystem(entities, offsets));
        Add(new SplitlineSystem());
        Add(new SplitlineBallsSystem());
        Add(new TrailAgeSystem<HitTrail>());
        Add(new NotePopSystem());
        Add(new DashLinkFadeSystem());
        Add(new TrailAgeSystem<DashTrail>());
        Add(new HitGhostSystem());
        Add(new ParticleBurstSystem(_particles));
        Add(new HudLayoutSystem(
            canvas,
            pointer ?? new NullPointerInputSource(),
            keyboard,
            hudLayout ?? new HudLayoutSettings(persist: false),
            _hudOverlay.Rid));
        Add(new ContactCounterSystem());
        Add(new WeaponHudSystem());
        Add(new PlayerSmoothingSystem());
        Add(new PlayerPathRecordSystem());
        Add(new PlayCameraSystem());
        Add(new PlayScoreSystem());
        Add(new ComboHudSystem());
        if (rigFiles is not null)
        {
            Add(new CharacterRigLoadSystem(rigFiles));
            Add(new CharacterMotionSystem());
            Add(new CharacterAttackSystem());
            Add(new CharacterRigAnimateSystem());
            Add(new CharacterRigDrawSystem(
                () => new SvgAttachmentVisuals(_characters.Rid),
                new CharacterRigGhosts(
                    _ghosts.Rid,
                    root => new SvgAttachmentVisuals(root, containLayers: true))));
        }

        if (divaToggle is not null)
            Add(new DivaNotesSystem(entities, divaToggle));
        Add(new PlayfieldDrawSystem(entities, canvas, _playfield.Rid, passes, toPixels));
        if (hitboxToggle is not null)
        {
            Add(new PlayfieldHitboxOverlaySystem(
                entities,
                canvas,
                _hitboxes.Rid,
                hitboxToggle));
        }
    }

    /// <summary>The playfield layer, which the editor also draws into.</summary>
    public Rid Playfield => _playfield.Rid;

    public void Dispose()
    {
        // The emitter and the child layers hold RIDs under the field, so they go
        // before the field itself.
        _particles.Dispose();
        _hitboxes.Dispose();
        _particleCanvas.Dispose();
        _characters.Dispose();
        _ghosts.Dispose();
        _playfield.Dispose();
        _hudOverlay.Dispose();
    }
}
