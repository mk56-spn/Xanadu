using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Audio;
using Xanadu.Core.Maths;
using Xanadu.Drawers.Editor;
using Xanadu.Logic.Editor;
using Xanadu.Screens.Editor;
using Xanadu.Storage.Editor;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Groups;
using Xanadu.Ui.Systems.Input;
using Xanadu.Ui.Systems.Ui;

namespace Xanadu.Wiring.Groups;

/// <summary>Editor-only systems, enabled while the Editor scene is active.</summary>
public sealed class EditorSystemGroup : SystemGroup, IDisposable
{
    private readonly EditorPlayfieldDrawSystem _draw;
    private readonly EditorAutosaveSystem _autosave;

    public EditorSystemGroup(
        EntityStore entities,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Handle playfieldCanvas,
        ITrackAudio metronomeAudio,
        EditorHistory history,
        IWheelSource? wheel = null,
        Func<bool>? autosaveToFile = null,
        ToastSystem? toasts = null) : base("Editor")
    {
        Add(new EditorCommandSystem(entities, history));
        Add(new EditorTimelineSystem(history));
        if (wheel is not null)
            Add(new TimelineSystemGroup(pointerInput, wheel));
        _autosave = new EditorAutosaveSystem(
            entities, LevelRecoveryStore.Default, saveToFile: autosaveToFile);
        Add(_autosave);
        Add(new EditorMetronomeSystem(metronomeAudio));
        Add(new EditorViewSystem(pointerInput));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorViewBarSystem(pointerInput));
        Add(new EditorPlacementSystem(entities));
        Add(new EditorToolGroupSystem(entities));
        Add(new EditorNoteTypeBarSystem(entities));
        Add(new EditorToolSectionSystem(entities));
        Add(new EditorButtonSystem(entities));
        Add(new EditorGroupPanelSystem(entities));
        Add(new EasePickerSystem());
        Add(new EditorMetadataSystem(entities));
        Add(new EditorHudSystem(entities, toasts));
        Add(new WaveformPreviewSystem());
        _draw = new EditorPlayfieldDrawSystem(entities, canvas, playfieldCanvas);
        Add(_draw);
        Add(new WarningPopupSystem(entities));
        Add(new EditorDialogSystem(entities));
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
    public void Dispose() => _autosave.EndSession();
}
