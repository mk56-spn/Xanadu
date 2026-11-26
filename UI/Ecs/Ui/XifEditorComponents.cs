using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Items;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>The settings of an item's common shader, as the host's compiler reports them.</summary>
public static partial class XifShaderParams
{
    public static IReadOnlyList<MaterialParam>? Common(Xif item)
    {
        string key = StackKey(item);
        if (key.Length == 0 || HostPlatform.DescribeMaterial is null)
            return null;
        IReadOnlyList<MaterialParam>? all = HostPlatform.DescribeMaterial(new CanvasMaterial(key));
        return all?.Where(param => !param.Name.StartsWith("xif_", StringComparison.Ordinal)).ToList();
    }
}

public static partial class XifShaderParams
{
    /// <summary>What the item's common shader and its layers are drawn as: one path, a stack's name, or "" for none.</summary>
    public static string StackKey(Xif item)
    {
        if (item.Get<XifShader>() is not { } module)
            return "";
        var paths = new List<string>(module.Layers.Count + 1);
        if (module.Shader.Scope == XifShaderScope.Common && !string.IsNullOrEmpty(module.Shader.Path))
            paths.Add(module.Shader.Path);
        paths.AddRange(module.Layers);
        return Xanadu.Core.Rendering.ShaderLayersKey.Of(paths);
    }
}

/// <summary>One shader a host offers: the label its row shows and the tab it's listed under.</summary>
public readonly record struct XifShaderChoice(string Label, XifShaderRef Shader, string Category = "");

/// <summary>A host's shader choices as the picker's tabs: one per category, in the order they first appear.</summary>
public static class XifShaderTabs
{
    /// <summary>The tabs, each listing its choices' labels in the host's order.</summary>
    public static PickerTab[] Of(IReadOnlyList<XifShaderChoice> choices)
    {
        List<string> names = [];
        List<List<string>> entries = [];
        foreach (XifShaderChoice choice in choices)
        {
            int tab = names.IndexOf(choice.Category);
            if (tab < 0)
            {
                tab = names.Count;
                names.Add(choice.Category);
                entries.Add([]);
            }
            entries[tab].Add(choice.Label);
        }
        return [.. names.Select((name, i) => new PickerTab(name, entries[i]))];
    }

    /// <summary>Which choice entry <paramref name="entry"/> of tab <paramref name="tab"/> is; -1 when there is none.</summary>
    public static int IndexOf(IReadOnlyList<XifShaderChoice> choices, int tab, int entry)
    {
        List<string> tabs = [];
        int count = 0;
        for (int i = 0; i < choices.Count; i++)
        {
            if (!tabs.Contains(choices[i].Category))
                tabs.Add(choices[i].Category);
            if (tabs.IndexOf(choices[i].Category) == tab && count++ == entry)
                return i;
        }
        return -1;
    }

    /// <summary>Where choice <paramref name="index"/> sits as (tab, entry); (-1, -1) for none.</summary>
    public static (int Tab, int Entry) PlaceOf(IReadOnlyList<XifShaderChoice> choices, int index)
    {
        if (index < 0 || index >= choices.Count)
            return (-1, -1);
        List<string> seen = [];
        int entry = 0;
        for (int i = 0; i <= index; i++)
        {
            if (!seen.Contains(choices[i].Category))
                seen.Add(choices[i].Category);
            if (i < index && choices[i].Category == choices[index].Category)
                entry++;
        }
        return (seen.IndexOf(choices[index].Category), entry);
    }
}

public enum XifEditorActionKind
{
    /// <summary>Picks the shape whose row was clicked (the command's arg).</summary>
    PickPolygon,

    /// <summary>Picks the shape whose swatch was clicked and opens or closes the colour picker for it.</summary>
    ToggleFill,

    PreviousPolygon,
    NextPolygon,
    AddPolygon,
    RemovePolygon,
    InsertPoint,
    RemovePoint,

    /// <summary>Moves the item's shader on to the host's next choice.</summary>

    /// <summary>Adds a quad, or a circle: primitives edited by their four handles.</summary>
    AddQuad,
    AddCircle,

    /// <summary>Adds a curve: quadratic Bézier segments, every on-curve and control point dragged on its own.</summary>
    AddCurve,

    /// <summary>Turns the shader's fitting to the shape on or off.</summary>
    ToggleFit,

    /// <summary>Takes the layer at the command's arg off the stack.</summary>
    RemoveLayer,

    /// <summary>Moves the layer at the command's arg one step up (drawn later) or down the stack.</summary>
    LayerUp,
    LayerDown,

    /// <summary>Closes the editor (Esc does too, where the host binds it).</summary>
    Done,
}

public enum XifEditorFieldKind
{
    Name,
}

/// <summary>A text field of one XIF editor; typed values are applied when it loses focus.</summary>
public struct XifEditorField(XifEditorFieldKind kind, Entity editor) : IComponent
{
    public XifEditorFieldKind Kind = kind;
    public Entity Editor = editor;
    public bool WasFocused;
}

/// <summary>What a drag in the shape box is moving.</summary>
public enum XifEditorDrag
{
    None,

    /// <summary>One point of the selected polygon.</summary>
    Point,

    /// <summary>The whole selected polygon.</summary>
    Polygon,
}

/// <summary>
/// A XIF editor, carried by its panel: whether it is
/// open, the panel's parts, the polygon and point selected, and the view of
/// the shape box. It opens over the host's work when asked
/// (<see cref="Xanadu.Ui.Systems.Ui.XifEditorSystem.Open"/>) and closes on
/// DONE, or by itself when the host has no item any more.
/// </summary>
public struct XifEditor : IComponent
{
    /// <summary>Where the panel is centred while it is open.</summary>
    public Vector2 Home;

    /// <summary>Shown over the host's work; closed, it is parked off screen.</summary>
    public bool Open;

    public int Polygon;
    public int Point;

    /// <summary>What the pointer is dragging in the shape box, if anything.</summary>
    public XifEditorDrag Drag;

    /// <summary>Where a polygon drag started, in item space, so the shape moves with the pointer.</summary>
    public Vector2 DragFrom;

    /// <summary>The fill colour picker is mid-drag, inside one gesture.</summary>
    public bool Colouring;

    /// <summary>The point under the pointer, or -1.</summary>
    public int HoverPoint;

    public bool Dragging => Drag != XifEditorDrag.None;

    /// <summary>The item-space box the shape view shows; held still while dragging so the view does not chase the pointer.</summary>
    public Rect2 View;

    public XifEditorParts Parts;

    /// <summary>Item space to screen as last drawn, in the box or on the host's placement: where a host that draws the item draws it.</summary>
    public Transform2D Shown;

    /// <summary>The shape box on screen as last drawn.</summary>
    public Rect2 ShownBox;

    /// <summary>One handle per point of the selected polygon, rebuilt when the count changes.</summary>
    public List<Entity> Handles;

    /// <summary>The last message, such as why an edit was refused.</summary>
    public string Status;

    /// <summary>The colour picker is open under the shape list, for the picked shape.</summary>
    public bool FillOpen;

    /// <summary>Each shape's row: the swatch showing its colour, and the button with its name.</summary>
    public List<(Entity Swatch, Entity Name)> ShapeRows;

    /// <summary>The shader the settings fields were built for, so they rebuild when it changes.</summary>
    public string SettingsFor;

    /// <summary>One value field per shader setting, with what it last showed and whether a scrub is open.</summary>
    public List<XifSettingField> SettingFields;

    /// <summary>The layers the layer rows were built for, so they rebuild when the stack changes.</summary>
    public string LayersFor;
}

/// <summary>A shader setting's value field in the XIF editor.</summary>
public sealed class XifSettingField(string name, Entity field, float[] shown)
{
    public string Name = name;
    public Entity Field = field;
    public float[] Shown = shown;
    public bool Dragging;

    /// <summary>The names of a setting's choices (<c>[Options]</c>), shown as a list to pick from; null for a value field.</summary>
    public IReadOnlyList<string>? Options;
}

/// <summary>The entities of an editor panel that its system writes to or reads.</summary>
public struct XifEditorParts
{
    /// <summary>The panel itself, which the owner's signals are emitted on.</summary>
    public Entity Panel;

    /// <summary>Dims everything behind the open editor and takes its clicks.</summary>
    public Entity Backdrop;

    /// <summary>The screen the backdrop covers.</summary>
    public Vector2 Screen;

    /// <summary>The layer the field lines draw on: above the panel's, and never faded.</summary>
    public Entity Lines;

    public Entity Title;
    public Entity Name;

    /// <summary>The column of shape rows, rebuilt when the shape count changes.</summary>
    public Entity ShapeList;

    /// <summary>The shape box: where the item is drawn filled and its points dragged.</summary>
    public Entity Canvas;

    /// <summary>Every polygon filled in its own colour, back to front; rebuilt when the count changes.</summary>
    public List<Entity> Fills;

    /// <summary>The selected polygon's outline, the origin mark, and where a click on an edge would add a point.</summary>
    public Entity Shape;
    public Entity Origin;
    public Entity EdgeGhost;

    public Entity PointLabel;

    /// <summary>The picked shape's colour (<see cref="UiColorField"/>), shown under the shape list once a swatch is clicked.</summary>
    public Entity Fill;

    /// <summary>The column holding the colour field and its picker, hidden until a swatch opens it.</summary>
    public Entity FillBox;

    public Entity Shader;

    /// <summary>The SETTINGS caption and the column its value fields are rebuilt in, one per shader setting.</summary>
    public Entity SettingsCaption;
    public Entity Settings;

    /// <summary>The LAYERS caption, the column of layer rows (rebuilt when the stack changes) and the list to add one from.</summary>
    public Entity LayersCaption;
    public Entity LayerList;
    public Entity LayerAdd;

    /// <summary>FIT TO SHAPE: shown when the shader has settings it can take from the shape.</summary>
    public Entity FitToggle;

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
        float scale = Math.Min(room.X / Math.Max(1f, view.Size.X), room.Y / Math.Max(1f, view.Size.Y));
        Vector2 offset = box.GetCenter() - view.GetCenter() * scale;
        return new Transform2D(0f, new Vector2(scale, scale), 0f, offset);
    }
}
