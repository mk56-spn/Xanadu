using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Editor;
using Writer.Systems.Input;

namespace Writer.Systems.Groups;

/// <summary>Editor playfield and inspector rendering.</summary>
public sealed class EditorSystemGroup : SystemGroup
{
    public EditorSystemGroup(
        EntityStore entities,
        IPointerInputSource pointerInput,
        IRectCanvas canvas,
        Rid editorCanvas,
        ITrackAudio audio,
        EditorHistory history) : base("Editor")
    {
        Add(new EditorPlaybackSystem(audio));
        Add(new EditorNoteInputSystem(entities, pointerInput, history));
        Add(new EditorPlayfieldDrawSystem(entities, canvas, editorCanvas));
    }
}
