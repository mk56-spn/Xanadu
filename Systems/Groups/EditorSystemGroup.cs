using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Audio;
using Writer.Systems.Canvas;
using Writer.Systems.Editor;

namespace Writer.Systems.Groups;

/// <summary>Editor playfield and inspector rendering.</summary>
public sealed class EditorSystemGroup : SystemGroup
{
    public EditorSystemGroup(
        IRectCanvas canvas,
        Rid editorCanvas,
        ITrackAudio audio) : base("Editor")
    {
        Add(new EditorPlaybackSystem(audio));
        Add(new EditorPlayfieldDrawSystem(canvas, editorCanvas));
    }
}
