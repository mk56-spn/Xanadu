using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Items;

namespace Xanadu.Ecs;

/// <summary>
/// What a tool gives the XIF editor: the item to edit, how edits are recorded,
/// and which shaders the item may use there. The editor never owns the item;
/// it asks for it every frame, so an undo that replaces the item is picked up.
/// </summary>
public interface IXifEditorHost
{
    /// <summary>The item being edited, or null to hide the editor.</summary>
    Xif? Item { get; }

    /// <summary>What the editor's title calls the item, such as the effect or note it belongs to.</summary>
    string Title { get; }

    /// <summary>The shaders a primitive may be drawn with here; the first is normally "none".</summary>
    IReadOnlyList<XifShaderChoice> Shaders { get; }

    /// <summary>Whether a texture path resolves where the item will be drawn, so the editor can warn about one that will not.</summary>
    bool CanLoad(string path);

    /// <summary>Runs one edit as one undo step and redraws whatever shows the item.</summary>
    void Edit(Action change);

    /// <summary>Opens a run of edits, such as a point drag, that undo as one step.</summary>
    void BeginGesture();

    /// <summary>Closes the run <see cref="BeginGesture"/> opened.</summary>
    void EndGesture();

    /// <summary>The item changed inside a gesture; redraw it without recording a step yet.</summary>
    void Changed();
}

/// <summary>One shader a host offers, with the label its button shows.</summary>
public readonly record struct XifShaderChoice(string Label, XifShaderRef Shader);

public enum XifEditorActionKind
{
    PreviousPrimitive,
    NextPrimitive,
    AddPolygon,
    RemovePrimitive,
    InsertPoint,
    RemovePoint,

    /// <summary>Moves the primitive's shader on to the host's next choice.</summary>
    NextShader,
}

public enum XifEditorFieldKind
{
    Name,
    Fill,
    Texture,
}

/// <summary>A button of one XIF editor, and which editor it belongs to.</summary>
public struct XifEditorAction(XifEditorActionKind kind, Entity editor) : IComponent
{
    public XifEditorActionKind Kind = kind;
    public Entity Editor = editor;
}

/// <summary>A text field of one XIF editor; typed values are applied when it loses focus.</summary>
public struct XifEditorField(XifEditorFieldKind kind, Entity editor) : IComponent
{
    public XifEditorFieldKind Kind = kind;
    public Entity Editor = editor;
    public bool WasFocused;
}

/// <summary>
/// A XIF editor, carried by its panel: the host it edits for, the panel's
/// parts, the primitive and point selected, and the view of the shape box.
/// </summary>
public struct XifEditor : IComponent
{
    public IXifEditorHost Host;

    /// <summary>Where the panel is centred while it has an item.</summary>
    public Vector2 Home;

    public int Primitive;
    public int Point;

    /// <summary>A point is being dragged in the shape box.</summary>
    public bool Dragging;

    /// <summary>The item-space box the shape view shows; held still while dragging so the view does not chase the pointer.</summary>
    public Rect2 View;

    public XifEditorParts Parts;

    /// <summary>One handle per point of the selected primitive, rebuilt when the count changes.</summary>
    public List<Entity> Handles;

    /// <summary>The last message, such as why an edit was refused.</summary>
    public string Status;
}

/// <summary>The entities of an editor panel that its system writes to or reads.</summary>
public struct XifEditorParts
{
    public Entity Title;
    public Entity Name;
    public Entity PrimitiveLabel;

    /// <summary>The shape box: where the item's outlines are drawn and its points dragged.</summary>
    public Entity Canvas;

    /// <summary>The other primitives' outlines, dim, then the selected one, and the origin mark.</summary>
    public Entity Others;
    public Entity Shape;
    public Entity Origin;

    public Entity PointLabel;
    public Entity Fill;
    public Entity Texture;
    public Entity Shader;
    public Entity Status;
}

/// <summary>Maps item space into an editor's shape box and back, keeping the shape's proportions.</summary>
public static class XifEditorView
{
    public const float Padding = 14f;

    /// <summary>Item space to screen: <paramref name="view"/> fitted, centred, inside <paramref name="box"/>.</summary>
    public static Transform2D ToScreen(Rect2 view, Rect2 box)
    {
        Vector2 room = (box.Size - new Vector2(Padding * 2, Padding * 2)).Max(Vector2.One);
        float scale = Mathf.Min(room.X / Mathf.Max(1f, view.Size.X), room.Y / Mathf.Max(1f, view.Size.Y));
        Vector2 offset = box.GetCenter() - view.GetCenter() * scale;
        return new Transform2D(0f, new Vector2(scale, scale), 0f, offset);
    }
}
