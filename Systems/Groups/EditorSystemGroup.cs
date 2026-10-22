using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Editor;
using Writer.Editor.Commands;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Editor;
using Writer.Systems.Input;

namespace Writer.Systems.Groups;

/// <summary>Editor-only systems, enabled while the Editor scene is active.</summary>
public sealed class EditorSystemGroup : SystemGroup
{
    public EditorSystemGroup(
        EntityStore entities,
        EditorCommandBus commands,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Rid playfieldCanvas,
        ITrackAudio metronomeAudio,
        EditorHistory history) : base("Editor")
    {
        Add(new EditorCommandSystem(entities, commands, history));
        Add(new EditorTimelineSystem());
        Add(new EditorMetronomeSystem(metronomeAudio));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorDropdownSystem(entities, commands));
        Add(new EditorButtonSystem(entities, commands));
        Add(new WaveformPreviewSystem());
        Add(new EditorPlayfieldDrawSystem(entities, canvas, playfieldCanvas));
        Add(new WarningPopupSystem(entities));
    }
}
