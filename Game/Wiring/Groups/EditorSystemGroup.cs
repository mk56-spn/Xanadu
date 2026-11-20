using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Drawers.Editor;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Groups;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;
using Xanadu.Logic.Audio;
using Xanadu.Logic.Editor;
using Xanadu.Logic.Editor.Commands;
using Xanadu.Logic.Editor.Recovery;
using Xanadu.Screens.Editor;

namespace Xanadu.Wiring.Groups;

/// <summary>Editor-only systems, enabled while the Editor scene is active.</summary>
public sealed class EditorSystemGroup : SystemGroup, IDisposable
{
    private readonly EditorPlayfieldDrawSystem _draw;
    private readonly EditorAutosaveSystem _autosave;

    public EditorSystemGroup(
        EntityStore entities,
        EditorCommandBus commands,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Handle playfieldCanvas,
        ITrackAudio metronomeAudio,
        EditorHistory history,
        IWheelSource? wheel = null,
        Func<bool>? autosaveToFile = null,
        ToastSystem? toasts = null) : base("Editor")
    {
        Add(new EditorCommandSystem(entities, commands, history));
        Add(new EditorTimelineSystem());
        if (wheel is not null)
            Add(new TimelineSystemGroup(pointerInput, wheel));
        _autosave = new EditorAutosaveSystem(
            entities, LevelRecoveryStore.Default, saveToFile: autosaveToFile);
        Add(_autosave);
        Add(new EditorMetronomeSystem(metronomeAudio));
        Add(new EditorViewSystem(pointerInput));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorViewBarSystem(pointerInput, commands));
        Add(new EditorPlacementSystem(entities));
        Add(new EditorToolGroupSystem(entities));
        Add(new EditorDashStrengthSystem(entities));
        Add(new EditorHoldLengthSystem(entities, history));
        Add(new EditorNoteTypeBarSystem(entities, commands));
        Add(new EditorToolSectionSystem(entities));
        Add(new EditorButtonSystem(entities, commands));
        Add(new EditorMetadataSystem(entities, commands));
        Add(new EditorHudSystem(entities, toasts));
        Add(new WaveformPreviewSystem());
        _draw = new EditorPlayfieldDrawSystem(entities, canvas, playfieldCanvas);
        Add(_draw);
        Add(new WarningPopupSystem(entities));
        Add(new EditorDialogSystem(entities, commands));
    }

    /// <summary>
    /// Enables the editor systems. Leaving the editor erases its last frame and
    /// its crash recovery copy, since leaving is not a crash.
    /// </summary>
    public void SetActive(bool active)
    {
        Enabled = active;
        if (!active)
        {
            _draw.Clear();
            _autosave.EndSession();
        }
    }

    /// <summary>Quitting normally is not a crash either.</summary>
    public void Dispose()
    {
        _autosave.EndSession();
    }
}
