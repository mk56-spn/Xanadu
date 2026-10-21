using System.Collections.Generic;

namespace Writer.Editor.Commands;

/// <summary>Single-writer queue between Godot UI and editor ECS systems.</summary>
public sealed class EditorCommandBus
{
    private readonly Queue<EditorCommand> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(EditorCommand command) => _queue.Enqueue(command);

    public bool TryDequeue(out EditorCommand command) =>
        _queue.TryDequeue(out command);
}
