using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ui.Editing;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A multi-line text editor: the text, its undo, where it draws, and what its
/// systems keep between frames. It knows nothing about what the text is; an
/// owner that does (a code editor) colours it, adds marks and takes keys first
/// by hearing the signals in <c>TextEditorSignals.cs</c>. <see cref="TextEditors.Create"/> makes one.
/// </summary>
/// <remarks>
/// <c>TextEditorInputSystem</c> turns keys, typing and the pointer into edits
/// for the focused one (<see cref="TextEditorFocused"/>); <c>TextEditorDrawSystem</c>
/// draws every one. Owners read <see cref="Buffer"/> and <see cref="Frame"/> after
/// the systems have run.
/// </remarks>
public struct TextEditor : IComponent
{
    public TextBuffer Buffer;
    public TextHistory History;

    /// <summary>What starts a line comment in this text (<c>//</c>), for Ctrl+/ and folding; null where there are none.</summary>
    public string? LineComment;

    /// <summary>
    /// The owner's own version, written on <see cref="TextVersionDue"/>: it moves whenever what the owner would
    /// prepare or decorate changes for a reason other than the text and the caret (problems from a compile, a
    /// rename), so the editor redraws only when something it draws from moved.
    /// </summary>
    public int OwnerVersion;

    /// <summary>The owner's answer to the last asking signal: it took the key, typing or press itself.</summary>
    public bool Taken;

    /// <summary>The owner's answer to <see cref="TextIndentAsked"/>; null leaves it to the indent steps.</summary>
    public int? Indent;

    /// <summary>
    /// The caret moves, selects and copies, but nothing edits: typing, cutting, pasting, undo and the
    /// editing keys are refused (<see cref="TextEditorFrame.Refused"/>), and the owner never hears them.
    /// </summary>
    public bool ReadOnly;

    /// <summary>The canvas item it draws into, clipped to its area.</summary>
    public Handle Item;

    /// <summary>Its area, scroll and the rows drawn last frame: where things are on screen.</summary>
    public TextEditorLayout Layout;

    /// <summary>What the input system did this frame.</summary>
    public TextEditorFrame Frame;

    /// <summary>Seconds since the caret last moved or the text changed: solid while moving, blinking once still.</summary>
    public float SinceCaretMove;

    /// <summary>A press began in the text and is still held: moving drags the selection.</summary>
    public bool Dragging;

    /// <summary>When and where the last click landed, and how many quick clicks led to it (1 place, 2 word, 3 line).</summary>
    public float LastClick;
    public (int Line, int Column) LastClickAt;
    public int Clicks;
}

/// <summary>The text editor that keys and typing go to.</summary>
public struct TextEditorFocused : ITag;

/// <summary>What happened to a text editor in the input system this frame, for its owner; <see cref="Refused"/>: an edit was tried on a read-only one.</summary>
public readonly record struct TextEditorFrame(bool Edited, bool CaretMoved, bool SteppedHistory, bool Refused = false);

/// <summary>A place in the text.</summary>
public readonly record struct TextCell(int Line, int Column);

/// <summary>Columns [<see cref="Start"/>, <see cref="End"/>) of a line drawn in a colour; text no run covers is plain.</summary>
public readonly record struct TextRun(int Start, int End, Color Colour);

/// <summary>
/// A label drawn in front of a column that isn't in the text (a parameter name);
/// the caret steps over it. With a <see cref="Swatch"/> it is drawn as a small
/// square of that colour instead, taking the label's room.
/// </summary>
public readonly record struct TextInlay(int Column, string Label, Color? Swatch = null)
{
    /// <summary>A colour square in front of a column, one column wide plus its gap.</summary>
    public static TextInlay ColourSwatch(int column, Color colour) => new(column, " ", colour);
}

/// <summary>
/// A line marked for attention: tinted, the word from <see cref="Underline"/>
/// underlined (-1 for none), and a note on a row of its own under it.
/// </summary>
public readonly record struct TextLineMark(Color Tint, Color Colour, int Underline = -1, string? Note = null);

/// <summary>What an owner adds to one line as it is drawn. Reused from line to line.</summary>
public sealed class TextLineDecor
{
    /// <summary>Colour runs in column order, not overlapping.</summary>
    public readonly List<TextRun> Runs = [];

    /// <summary>Inlays in column order.</summary>
    public readonly List<TextInlay> Inlays = [];

    public TextLineMark? Mark;

    public void Clear()
    {
        Runs.Clear();
        Inlays.Clear();
        Mark = null;
    }
}

/// <summary>Making and focusing text editors.</summary>
public static class TextEditors
{
    /// <summary>A text editor over <paramref name="area"/>, drawing into a new canvas item under <paramref name="parent"/>, focused.</summary>
    public static Entity Create(EntityStore store, Handle parent, Rect2 area, string text)
    {
        Handle item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, parent);
        CanvasServer.Current.SetCustomRect(item, true, area);
        CanvasServer.Current.SetClip(item, true);

        var buffer = new TextBuffer();
        buffer.Load(text);
        Entity entity = store.CreateEntity(new TextEditor
        {
            Buffer = buffer,
            History = new TextHistory(buffer),
            Item = item,
            Layout = new TextEditorLayout(area),
            LastClick = -1,
            LastClickAt = (-1, -1),
        });
        entity.GetComponent<TextEditor>().Layout.Editor = entity;
        entity.AddTag<TextEditorFocused>();
        return entity;
    }

    /// <summary>
    /// A text editor on a laid-out node: it takes its area from the node's box
    /// every frame (<see cref="Follow"/>), so it moves and resizes with the
    /// layout in the same frame. Draws into a new canvas item under <paramref name="parent"/>, focused.
    /// </summary>
    public static Entity Create(UiNode node, Handle parent, string text)
    {
        Rect2 area = UiLayout.Box(node.Entity);
        Handle item = CanvasServer.Current.CreateItem();
        CanvasServer.Current.SetParent(item, parent);
        CanvasServer.Current.SetCustomRect(item, true, area);
        CanvasServer.Current.SetClip(item, true);

        var buffer = new TextBuffer();
        buffer.Load(text);
        node.Entity.AddComponent(new TextEditor
        {
            Buffer = buffer,
            History = new TextHistory(buffer),
            Item = item,
            Layout = new TextEditorLayout(area) { Editor = node.Entity },
            LastClick = -1,
            LastClickAt = (-1, -1),
        });
        node.Entity.AddTag<TextEditorFocused>();
        return node.Entity;
    }

    /// <summary>An editor on a laid-out node takes the node's box as its area (and its item's clip); a free one keeps its own.</summary>
    public static void Follow(Entity entity, ref TextEditor editor)
    {
        if (!entity.HasComponent<Rectangle>() || !entity.HasComponent<Core.Position>())
            return;

        Rect2 area = UiLayout.Box(entity);
        if (area == editor.Layout.Area)
            return;

        editor.Layout.Area = area;
        CanvasServer.Current.SetCustomRect(editor.Item, true, area);
    }

    /// <summary>Removes a text editor and its canvas item; one made on a node leaves the node to its owner.</summary>
    public static void Delete(Entity entity)
    {
        if (entity.IsNull)
            return;
        CanvasServer.Current.Free(entity.GetComponent<TextEditor>().Item);
        if (entity.HasComponent<Rectangle>())
        {
            entity.RemoveComponent<TextEditor>();
            entity.RemoveTag<TextEditorFocused>();
            return;
        }

        entity.DeleteEntity();
    }
}
