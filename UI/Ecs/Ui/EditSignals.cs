namespace Xanadu.Ui.Ecs.Ui;

// Signals a shared editor panel (the XIF editor, the particle editor) emits on
// itself for the edits it makes on its owner's data. The owner hears them with
// AddSignalHandler and records them on its own undo.

/// <summary>
/// Run <paramref name="Change"/> as one undo step and redraw whatever shows the data. The change is
/// code, not data, while what the editors edit (Xif items, particle settings) are C# objects outside
/// the ECS; it becomes entity edits once they move in.
/// </summary>
public readonly record struct EditAsked(Action Change);

/// <summary>A run of edits begins, such as a drag, to undo as one step.</summary>
public readonly record struct EditGestureStarted;

/// <summary>The run <see cref="EditGestureStarted"/> began is over.</summary>
public readonly record struct EditGestureEnded;

/// <summary>The data changed inside a gesture: redraw it without recording a step yet.</summary>
public readonly record struct EditChanged;
