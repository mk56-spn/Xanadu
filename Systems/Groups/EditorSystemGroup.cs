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
public sealed class EditorSystemGroup : SystemGroup
{
    public EditorSystemGroup(
        EntityStore entities,
        EditorCommandBus commands,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Rid playfieldCanvas,
        Rid groupPreviewCanvas,
        ITrackAudio metronomeAudio,
        EditorHistory history) : base("Editor")
    {
        Add(new EditorCommandSystem(entities, commands, history));
        Add(new EditorTimelineSystem());
        Add(new EditorAutosaveSystem(entities, LevelRecoveryStore.Default));
        Add(new EditorMetronomeSystem(metronomeAudio));
        Add(new EditorGroupTargetSystem(entities, pointerInput, history));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorDropdownSystem(entities, commands));
        Add(new EditorSidebarLayoutSystem(entities));
        Add(new EditorGroupSystem(entities, history, pointerInput, commands));
        Add(new EditorGroupPreviewSystem(entities, canvas, groupPreviewCanvas));
        Add(new EditorPlacementSystem(entities));
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
}
