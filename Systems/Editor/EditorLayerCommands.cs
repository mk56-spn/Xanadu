using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;

namespace Xanadu.Systems.Editor;

/// <summary>The editor's layer commands: moving the selection forward or back through its layer, as one undo step.</summary>
public sealed class EditorLayerCommands(EditorCommandContext context)
{
    public bool TryApply(EditorCommand command, Entity editor)
    {
        if (command.Type != EditorCommandType.MoveInLayer)
            return false;

        if (!editor.HasComponent<NoteSelection>())
            return true;

        EditorSnapshot before = context.History.Capture();
        if (EditorLayerEdits.Move(context.Entities, editor.GetComponent<NoteSelection>().AllNotes, command.IntValue))
            context.History.Record(before, context.History.Capture());
        return true;
    }
}
