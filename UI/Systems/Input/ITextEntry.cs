namespace Xanadu.Systems.Input;

/// <summary>
/// Typed characters and the clipboard, for UI that edits text itself (the
/// <see cref="Xanadu.Ecs.TextEditor"/>) rather than through a native text field.
/// </summary>
public interface ITextEntry
{
    /// <summary>
    /// While true, characters typed wait in <see cref="TakeTyped"/> instead of
    /// going to a focused <see cref="Xanadu.Ecs.TextField"/>.
    /// </summary>
    bool Claimed { get; set; }

    /// <summary>What was typed since the last call (layout and IME resolved), while claimed.</summary>
    string TakeTyped();

    /// <summary>The system clipboard's text; empty when it holds none.</summary>
    string Clipboard { get; set; }
}

/// <summary>Nothing typed, a clipboard of its own: for tests and hosts without text input.</summary>
public sealed class NoTextEntry : ITextEntry
{
    public bool Claimed { get; set; }

    public string TakeTyped() => "";

    public string Clipboard { get; set; } = "";
}
