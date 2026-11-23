using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Logic.Editor;

/// <summary>
/// What every editor command handler works with: the store, the undo history,
/// the intents heard since the command system last ran, and the edits that
/// must wait until those have run.
/// </summary>
public sealed class EditorCommandContext(EntityStore entities, EditorHistory history)
{
    private readonly List<(Entity Editor, Action<Entity, CommandBuffer> Apply)> _heard = [];

    public EntityStore Entities { get; } = entities;

    public EditorHistory History { get; } = history;

    /// <summary>Edits that change existing entities, run once every heard intent is applied.</summary>
    public List<Action> AfterQuery { get; } = [];

    /// <summary>
    /// Hears <typeparamref name="T"/> on <paramref name="editor"/>. It is applied
    /// on the command system's next update, in the order heard, so a press
    /// emitted mid-frame never edits the level under another system's loop.
    /// </summary>
    public void On<T>(Entity editor, Action<Entity, T, CommandBuffer> apply)
        where T : struct, IIntent<T>
    {
        editor.AddSignalHandler<T>(signal =>
        {
            T intent = signal.Event;
            _heard.Add((signal.Entity, (on, buffer) => apply(on, intent, buffer)));
        });
    }

    /// <summary>
    /// <see cref="On{T}(Entity, Action{Entity, T, CommandBuffer})"/> for intents that
    /// only touch the editor's own components.
    /// </summary>
    public void On<T>(Entity editor, Action<Entity, T> apply)
        where T : struct, IIntent<T> =>
        On<T>(editor, (on, intent, _) => apply(on, intent));

    /// <summary>Applies what was heard, skipping editors that are gone, then the deferred edits.</summary>
    public void Run(CommandBuffer buffer)
    {
        // A handler may emit again; what it emits waits for the next update.
        var heard = _heard.ToArray();
        _heard.Clear();
        foreach ((Entity editor, Action<Entity, CommandBuffer> apply) in heard)
        {
            if (!editor.IsNull)
                apply(editor, buffer);
        }

        foreach (Action edit in AfterQuery)
            edit();

        AfterQuery.Clear();
    }
}
