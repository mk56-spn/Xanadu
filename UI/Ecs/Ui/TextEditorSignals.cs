using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Editing;

namespace Xanadu.Ui.Ecs.Ui;

// Signals a text editor emits on its own entity. An owner that knows what the
// text is (a code editor) hears the ones it adds to plain text; the rest go
// unheard and the editor treats the text as plain. The asking ones are answered
// by writing TextEditor.Taken or TextEditor.Indent (TextEditors.Take, TextEditors.AnswerIndent).

/// <summary>Write <see cref="TextEditor.OwnerVersion"/> now; emitted before every draw.</summary>
public readonly record struct TextVersionDue;

/// <summary>Before drawing, when anything drawn changed: work done for the whole text, and cells to box (a matching bracket pair).</summary>
public readonly record struct TextPrepare(TextBuffer Buffer, List<TextCell> Boxed);

/// <summary>Fill one line's colours, inlays and mark. Emitted for lines as they are drawn or hit, so keep the handler cheap.</summary>
public readonly record struct TextDecorate(TextBuffer Buffer, int Line, TextLineDecor Decor);

/// <summary>A key press (or a held key repeating) before the editor's own handling; asks.</summary>
public readonly record struct TextKey(TextBuffer Buffer, Key Key, bool Shift, bool Ctrl);

/// <summary>Characters typed, before they are inserted; asks whether the owner put them in itself.</summary>
public readonly record struct TextTyped(TextBuffer Buffer, string Text);

/// <summary>A press inside the editor's area before it places the caret (a popup over the text); asks.</summary>
public readonly record struct TextPressed(TextBuffer Buffer, Vector2 Point);

/// <summary>A Ctrl+click, once the caret is placed at it (go to what is there); asks.</summary>
public readonly record struct TextCtrlClicked(TextBuffer Buffer);

/// <summary>
/// The spaces <paramref name="Line"/> belongs at by the code's nesting, for a line off the indent steps
/// and for Enter from one; answered with <see cref="TextEditors.AnswerIndent"/>.
/// </summary>
public readonly record struct TextIndentAsked(TextBuffer Buffer, int Line);

/// <summary>Asking an editor's owner and answering it.</summary>
public static class TextEditorAsking
{
    /// <summary>Emits an asking signal on <paramref name="editor"/>; true when its owner took it.</summary>
    public static bool Asks<T>(this Entity editor, T signal) where T : struct
    {
        editor.GetComponent<TextEditor>().Taken = false;
        editor.EmitSignal(signal);
        return editor.GetComponent<TextEditor>().Taken;
    }

    /// <summary>The owner's answer to an asking signal: it handled it itself.</summary>
    public static void Take(this Entity editor) => editor.GetComponent<TextEditor>().Taken = true;

    /// <summary>The owner's answer to <see cref="TextIndentAsked"/>.</summary>
    public static void AnswerIndent(this Entity editor, int? spaces) => editor.GetComponent<TextEditor>().Indent = spaces;

    /// <summary>Where a line's text belongs (<see cref="TextIndent.Wanted"/>), asking the owner when it is off the steps.</summary>
    public static int WantedIndent(this Entity editor, TextBuffer buffer, int line) => WantedIndent(buffer, line, (_, _) =>
    {
        editor.GetComponent<TextEditor>().Indent = null;
        editor.EmitSignal(new TextIndentAsked(buffer, line));
        return editor.GetComponent<TextEditor>().Indent;
    });

    /// <summary>Where a line's text belongs, asking <paramref name="indentFor"/> only when it is off the steps.</summary>
    public static int WantedIndent(TextBuffer buffer, int line, Func<TextBuffer, int, int?> indentFor)
    {
        string text = buffer.Lines[line];
        return TextIndent.Wanted(text, TextIndent.Of(text) % TextBuffer.Indent != 0 ? indentFor(buffer, line) : null);
    }
}
