using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// The XIF editor as its owner describes it: the item to edit, and which
/// shaders it may use there. The owner writes it whenever
/// <see cref="XifEditorFrameDue"/> is emitted on the panel, so an undo that
/// replaces the item is picked up; the editor never owns the item. Edits go
/// back as the shared edit signals (<see cref="EditAsked"/> and the gesture
/// ones), which the owner records on its own undo.
/// </summary>
public struct XifEditorFrame() : IComponent
{
    /// <summary>The item being edited, or null to hide the editor.</summary>
    public Xif? Item;

    /// <summary>What the editor's title calls the item, such as the effect or note it belongs to.</summary>
    public string Title = "";

    /// <summary>The shaders the item may be drawn with here; the first is normally "none". Fixed when the panel is built.</summary>
    public IReadOnlyList<XifShaderChoice> Shaders = [];

    /// <summary>The common shaders that may be stacked over the item's shader here (<see cref="XifShader.Layers"/>); none hides LAYERS. Fixed when the panel is built.</summary>
    public IReadOnlyList<XifShaderChoice> Layers = [];

    /// <summary>
    /// How solid the editor's extra backgrounds draw, 0 to 1: the dimming over
    /// the rest of the screen and the shape box fade with it. The editor's own
    /// see-through backing, its outline, the shapes, the field lines and the
    /// controls always draw as they are.
    /// </summary>
    public float Opacity = 1f;

    /// <summary>
    /// Where the item is on screen, item space to screen: the field lines sit
    /// there and points are dragged there, such as on the bone a part rides.
    /// The owner is drawing the item there itself, through its shader, so the
    /// editor draws no fills of its own over it. Null fits the item in the
    /// editor's shape box, drawn with plain fills.
    /// </summary>
    public Transform2D? Placement;

    /// <summary>
    /// The owner draws the item itself, through its shader, wherever the editor
    /// shows it: on its <see cref="Placement"/>, or fitted in the shape box at
    /// <see cref="XifEditor.Shown"/>. The editor then draws no fills of its
    /// own, only the field lines, the outline and the points over the owner's
    /// drawing. False: the editor fills the shapes in the box itself, plainly.
    /// A placed item is always the owner's to draw.
    /// </summary>
    public bool DrawsItem;

    /// <summary>Asks the owner of the editor on <paramref name="panel"/> to describe it now, and reads what it wrote.</summary>
    public static XifEditorFrame Refresh(Entity panel)
    {
        panel.EmitSignal(new XifEditorFrameDue());
        return panel.GetComponent<XifEditorFrame>();
    }
}

// The XIF editor emits this and the shared edit signals (EditSignals.cs) on its panel; the owner hears them.

/// <summary>Write <see cref="XifEditorFrame"/> now.</summary>
public readonly record struct XifEditorFrameDue;

/// <summary>Emitting an editor's signals from the code that holds its component.</summary>
public static class XifEditorAsk
{
    /// <summary>The frame as the owner last wrote it, without asking again.</summary>
    public static XifEditorFrame Shown(in XifEditor editor) => editor.Parts.Panel.GetComponent<XifEditorFrame>();

    /// <summary>Asks the owner to describe the editor now.</summary>
    public static XifEditorFrame Refresh(in XifEditor editor) => XifEditorFrame.Refresh(editor.Parts.Panel);

    public static void Edit(in XifEditor editor, Action change) => editor.Parts.Panel.EmitSignal(new EditAsked(change));

    public static void BeginGesture(in XifEditor editor) => editor.Parts.Panel.EmitSignal(new EditGestureStarted());

    public static void EndGesture(in XifEditor editor) => editor.Parts.Panel.EmitSignal(new EditGestureEnded());

    public static void Changed(in XifEditor editor) => editor.Parts.Panel.EmitSignal(new EditChanged());
}
