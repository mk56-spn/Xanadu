using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using System;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Systems.Audio;
using Xanadu.Model.Settings;
using Xanadu.Systems.Input;
using Xanadu.Systems.Particles;
using Xanadu.Systems.Playfields;
using Xanadu.Drawers.Playfields;
using Xanadu.Systems.Replays;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Systems.Groups;

/// <summary>
/// Shared playback and playfield rendering for editor and play scenes. Owns the
/// playfield's layer tree: the field itself, particle bursts, the F6 hitbox
/// outlines, and the HUD edit overlay.
/// </summary>
public sealed class PlayfieldSystemGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _playfield;
    private readonly CanvasItemHandle _particleCanvas;
    private readonly CanvasItemHandle _hitboxes;
    private readonly CanvasItemHandle _hudOverlay;
    private readonly IParticleEmitter _particles;

    public PlayfieldSystemGroup(
        EntityStore entities,
        IRectCanvas canvas,
        Handle parentCanvas,
        ITrackAudio audio,
        KeybindSettings keybinds,
        IKeyboardState keyboard,
        OffsetSettings offsets,
        IPointerInputSource? pointer = null,
        HudLayoutSettings? hudLayout = null,
        ReplayKeyboard? replayKeyboard = null,
        IOverlayToggleSource? hitboxToggle = null,
        MapOffsetSettings? mapOffsets = null,
        Xanadu.Drawers.Passes.IPassSurfaces? passes = null,
        Func<Rect2, Rect2>? toPixels = null) : base("Playfield")
    {
        _playfield = new CanvasItemHandle(parentCanvas, 800);

        // Particle bursts: over the field.
        _particleCanvas = new CanvasItemHandle(_playfield.Handle, 0);
        _particles = HostPlatform.Particles is { } server
            ? new CanvasParticleEmitter(_particleCanvas.Handle, server)
            : new SilentParticleEmitter();

        // The F6 hitbox outlines: inside the playfield's layer so the HUD stays on top.
        _hitboxes = new CanvasItemHandle(_playfield.Handle, 0, visible: false);

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

        // The square follows the curve notes whose heads were hit; heads are judged like any action note.
        Add(new CurvePlayerSystem());
        Add(new PlayNoteInputSystem(entities, keybinds, keyboard, offsets));
        Add(new TrailAgeSystem<HitTrail>());
        Add(new NotePopSystem());
        Add(new ParticleBurstSystem(_particles));
        Add(new HudLayoutSystem(
            canvas,
            pointer ?? new NullPointerInputSource(),
            keyboard,
            hudLayout ?? new HudLayoutSettings(persist: false),
            _hudOverlay.Handle));
        Add(new PlayScoreSystem());
        Add(new ComboHudSystem());
        Add(new PlayCameraSystem());
        Add(new ShaderPlaySystem());

        Add(new PlayfieldDrawSystem(entities, canvas, _playfield.Handle, passes, toPixels));
        if (hitboxToggle is not null)
        {
            Add(new PlayfieldHitboxOverlaySystem(
                entities,
                canvas,
                _hitboxes.Handle,
                hitboxToggle));
        }
    }

    /// <summary>The playfield layer, which the editor also draws into.</summary>
    public Handle Playfield => _playfield.Handle;

    public void Dispose()
    {
        // The emitter and the child layers hold RIDs under the field, so they go
        // before the field itself.
        (_particles as IDisposable)?.Dispose();
        _hitboxes.Dispose();
        _particleCanvas.Dispose();
        _playfield.Dispose();
        _hudOverlay.Dispose();
    }
}
