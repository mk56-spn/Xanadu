using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Editor.Recovery;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Editor;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Groups;

/// <summary>Editor-only systems, enabled while the Editor scene is active.</summary>
public sealed class EditorSystemGroup : SystemGroup, IDisposable
{
    private readonly CanvasItemHandle _groupOverlay;
    private readonly EditorPlayfieldDrawSystem _draw;
    private readonly EditorAutosaveSystem _autosave;

    public EditorSystemGroup(
        EntityStore entities,
        EditorCommandBus commands,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Rid playfieldCanvas,
        Rid parentCanvas,
        ITrackAudio metronomeAudio,
        EditorHistory history,
        IWheelSource? wheel = null,
        Xanadu.Systems.Shaders.IPassSurfaces? passes = null,
        Func<Rect2, Rect2>? toPixels = null,
        Func<bool>? autosaveToFile = null,
        Xanadu.Systems.Ui.ToastSystem? toasts = null) : base("Editor")
    {
        // The group panel's drawn timeline sits over its controls and under the
        // bar's drop-downs; only the editor scene shows it.
        _groupOverlay = new CanvasItemHandle(parentCanvas, Xanadu.Ecs.UiLayers.EditorGroupsCanvas, visible: false);

        Add(new EditorCommandSystem(entities, commands, history));
        Add(new EditorTimelineSystem());
        if (wheel is not null)
            Add(new TimelineSystemGroup(pointerInput, wheel));
        _autosave = new EditorAutosaveSystem(
            entities, LevelRecoveryStore.Default, saveToFile: autosaveToFile);
        Add(_autosave);
        Add(new EditorMetronomeSystem(metronomeAudio));
        Add(new EditorViewSystem(pointerInput));
        Add(new EditorGroupTargetSystem(entities, pointerInput, history));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorDropdownSystem(entities, commands, history));
        Add(new EditorGroupSystem(entities, history, pointerInput, commands));
        Add(new EditorViewBarSystem(pointerInput, commands));
        Add(new EditorSelfModulateSystem(history));
        Add(new EditorBlockOutlineSystem(history));
        Add(new EditorTextPanelSystem(history));
        Add(new EditorGrassPanelSystem(history));
        Add(new EditorFlarePanelSystem(history));
        Add(new EditorLayerInspectorSystem(history));
        Add(new EditorAlignSystem(history));
        Add(new EditorLayersPanelSystem(history));
        Add(new EditorGroupOverlayDrawSystem(canvas, _groupOverlay.Rid));
        Add(new EditorPlacementSystem(entities));
        Add(new EditorBlockSizeSystem(entities));
        Add(new EditorToolGroupSystem(entities));
        Add(new EditorXifHostSystem(history));
        Add(new Xanadu.Systems.Ui.XifEditorSystem(pointerInput));
        Add(new EditorDashStrengthSystem(entities));
        Add(new EditorContactCostSystem(entities));
        Add(new EditorNoteTypeBarSystem(entities, commands));
        Add(new EditorToolSectionSystem(entities));
        Add(new EditorButtonSystem(entities, commands));
        Add(new EditorMetadataSystem(entities, commands));
        Add(new EditorHudSystem(entities, toasts));
        Add(new WaveformPreviewSystem());
        Add(new EditorAnalysisSystem(entities));
        _draw = new EditorPlayfieldDrawSystem(entities, canvas, playfieldCanvas, passes, toPixels);
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
        _groupOverlay.SetVisible(active);
        if (!active)
        {
            _draw.Clear();
            _draw.ReleasePasses();
            _autosave.EndSession();
        }
    }

    /// <summary>Quitting normally is not a crash either.</summary>
    public void Dispose()
    {
        _autosave.EndSession();
        _groupOverlay.Dispose();
    }
}
