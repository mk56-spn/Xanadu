using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;

namespace Xanadu.Logic.Editor;

/// <summary>
/// Applies the editor's intents (<c>Xanadu.Model.Editor.Intents</c>), emitted
/// on the editor entity by its buttons, shortcuts, fields and timeline. The
/// first time it sees an editor it has the level, selection and group handlers
/// listen on it; what they hear is applied here, once a frame.
/// </summary>
public sealed class EditorCommandSystem :
    QuerySystem<LevelInfo, Playfield, Playhead, EditorMetronome>
{
    private readonly EditorCommandContext _context;
    private readonly EditorLevelCommands _levels;
    private readonly EditorSelectionCommands _selection;
    private readonly EditorGroupCommands _groups;

    /// <summary>Editors already listened to, by id and revision: a rebuilt scene may reuse an id.</summary>
    private readonly HashSet<(int Id, int Revision)> _listening = [];

    public EditorCommandSystem(EntityStore entities, EditorHistory history)
    {
        _context = new EditorCommandContext(entities, history);
        _levels = new EditorLevelCommands(_context);
        _selection = new EditorSelectionCommands(_context);
        _groups = new EditorGroupCommands(_context);
    }

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.Entities)
        {
            if (_listening.Add((editor.Id, editor.Revision)))
                Listen(editor);
        }

        _context.Run(CommandBuffer);
    }

    private void Listen(Entity editor)
    {
        // Restoring a snapshot makes structural changes, so it waits for the rest.
        _context.On<Undo>(editor, (on, _) => _context.AfterQuery.Add(() =>
        {
            if (_context.History.Undo())
                EditorSelectionCommands.ClearSelection(on);
        }));
        _context.On<Redo>(editor, (on, _) => _context.AfterQuery.Add(() =>
        {
            if (_context.History.Redo())
                EditorSelectionCommands.ClearSelection(on);
        }));

        _levels.Listen(editor);
        _selection.Listen(editor);
        _groups.Listen(editor);
    }
}
