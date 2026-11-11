using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Single ECS writer for editor intents from the Godot UI. It owns the command
/// queue and the deferred edits; the level, selection, shader and layer handlers own
/// what each command does.
/// </summary>
public sealed class EditorCommandSystem :
    QuerySystem<LevelInfo, Playfield, Playhead, EditorMetronome>
{
    private readonly EditorCommandBus _commands;
    private readonly EditorCommandContext _context;
    private readonly EditorLevelCommands _levels;
    private readonly EditorSelectionCommands _selection;
    private readonly EditorShaderCommands _shaders;
    private readonly EditorLayerCommands _layers;

    public EditorCommandSystem(
        EntityStore entities,
        EditorCommandBus commands,
        EditorHistory history)
    {
        _commands = commands;
        _context = new EditorCommandContext(entities, history);
        _levels = new EditorLevelCommands(_context);
        _selection = new EditorSelectionCommands(_context);
        _shaders = new EditorShaderCommands(_context);
        _layers = new EditorLayerCommands(_context);
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((
            ref level,
            ref playfield,
            ref playhead,
            ref metronome,
            entity) =>
        {
            while (_commands.TryDequeue(out EditorCommand command))
                Apply(command, entity);
        });

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

        if (_levels.TryApply(command, editor, CommandBuffer) ||
            _selection.TryApply(command, editor, CommandBuffer) ||
            _shaders.TryApply(command, editor) ||
            _layers.TryApply(command, editor))
        {
            return;
        }
    }
}
