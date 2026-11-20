using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Ecs.Components;
using Xanadu.Logic.Editor.Commands;

namespace Xanadu.Logic.Editor;

/// <summary>
/// Single ECS writer for editor intents from the UI. It owns the command queue
/// and the deferred edits; the level and selection handlers own what each
/// command does.
/// </summary>
public sealed class EditorCommandSystem :
    QuerySystem<LevelInfo, Playfield, Playhead, EditorMetronome>
{
    private readonly EditorCommandBus _commands;
    private readonly EditorCommandContext _context;
    private readonly EditorLevelCommands _levels;
    private readonly EditorSelectionCommands _selection;

    public EditorCommandSystem(
        EntityStore entities,
        EditorCommandBus commands,
        EditorHistory history)
    {
        _commands = commands;
        _context = new EditorCommandContext(entities, history);
        _levels = new EditorLevelCommands(_context);
        _selection = new EditorSelectionCommands(_context);
    }

    protected override void OnUpdate()
    {
        Query.EachEntity(new Drain(this, Query.Store));

        foreach (Action edit in _context.AfterQuery)
            edit();

        _context.AfterQuery.Clear();
    }

    private void Apply(EditorCommand command, Entity editor)
    {
        // Restoring a snapshot makes structural changes, so it waits for the query.
        if (command.Type is EditorCommandType.Undo or EditorCommandType.Redo)
        {
            bool undo = command.Type == EditorCommandType.Undo;
            _context.AfterQuery.Add(() =>
            {
                bool changed = undo ? _context.History.Undo() : _context.History.Redo();
                if (changed)
                    EditorSelectionCommands.ClearSelection(editor);
            });
            return;
        }

        if (!_levels.TryApply(command, editor, CommandBuffer))
            _selection.TryApply(command, editor, CommandBuffer);
    }

    private readonly record struct Drain(EditorCommandSystem System, EntityStore Store)
        : IEachEntity<LevelInfo, Playfield, Playhead, EditorMetronome>
    {
        public void Execute(ref LevelInfo level, ref Playfield playfield, ref Playhead playhead, ref EditorMetronome metronome, int id)
        {
            while (System._commands.TryDequeue(out EditorCommand command))
                System.Apply(command, Store.GetEntityById(id));
        }
    }
}
