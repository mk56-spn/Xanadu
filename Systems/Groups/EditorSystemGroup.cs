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
    private readonly CanvasItemHandle _groupPreview;

    public EditorSystemGroup(
        EntityStore entities,
        EditorCommandBus commands,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Rid playfieldCanvas,
        Rid parentCanvas,
        ITrackAudio metronomeAudio,
        EditorHistory history) : base("Editor")
    {
        // The group overlay's live preview draws above the dialog but below the
        // debug overlay; only the editor scene shows it.
        _groupPreview = new CanvasItemHandle(parentCanvas, 985, visible: false);

        Add(new EditorCommandSystem(entities, commands, history));
        Add(new EditorTimelineSystem());
        Add(new EditorAutosaveSystem(entities, LevelRecoveryStore.Default));
        Add(new EditorMetronomeSystem(metronomeAudio));
        Add(new EditorGroupTargetSystem(entities, pointerInput, history));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorDropdownSystem(entities, commands));
        Add(new EditorGroupSystem(entities, history, pointerInput, commands));
        Add(new EditorGroupPreviewSystem(entities, canvas, _groupPreview.Rid));
        Add(new EditorPlacementSystem(entities));
        Add(new EditorDashStrengthSystem(entities));
        Add(new EditorContactCostSystem(entities));
        Add(new EditorRailLayerSystem(entities, history));
        Add(new EditorShapeSystem(entities, history));
        Add(new EditorNoteTypeBarSystem(entities, commands));
        Add(new EditorDirectionWheelSystem());
        Add(new EditorButtonSystem(entities, commands));
        Add(new EditorMetadataSystem(entities, commands));
        Add(new EditorHudSystem(entities));
        Add(new WaveformPreviewSystem());
        Add(new EditorAnalysisSystem(entities));
        Add(new EditorPlayfieldDrawSystem(entities, canvas, playfieldCanvas));
        Add(new WarningPopupSystem(entities));
    }

    /// <summary>Enables the editor systems and shows or hides its preview layer.</summary>
    public void SetActive(bool active)
    {
        Enabled = active;
        _groupPreview.SetVisible(active);
    }

    public void Dispose() => _groupPreview.Dispose();
}
