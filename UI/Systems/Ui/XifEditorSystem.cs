using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Systems.Input;
using Xanadu.Theme;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Runs every XIF editor panel: shows it while its host has an item, applies
/// its buttons and fields, lets points be dragged in its shape box, and keeps
/// its readouts and outlines in step with the item. Every edit goes through
/// the host, which records it however its tool does undo.
/// </summary>
public sealed class XifEditorSystem(IPointerInputSource pointer, IVisualRidFactory? visuals = null)
    : QuerySystem<XifEditor>
{
    /// <summary>A press this close to a point, in screen pixels, grabs it.</summary>
    public const float GrabRadius = 9f;

    private const float HandleSize = 7f;
    private static readonly Color Handle = Palette.Accent;
    private static readonly Color SelectedHandle = Colors.White;

    private readonly HashSet<int> _held = new();
    private bool _wasPressed;

    protected override void OnUpdate()
    {
        PointerInput input = pointer.Read();
        bool pressedNow = input.Pressed && !_wasPressed;
        _wasPressed = input.Pressed;

        foreach (Entity panel in Query.ToEntityList())
            Update(panel, input, pressedNow);
    }

    private void Update(Entity panel, PointerInput input, bool pressedNow)
    {
        ref XifEditor editor = ref panel.GetComponent<XifEditor>();
        Xif? item = editor.Host.Item;
        if (item is null)
        {
            if (editor.Dragging)
                editor.Host.EndGesture();
            editor.Dragging = false;
            Place(panel, UiLayout.Hidden);
            HideOutlines(ref editor);
            return;
        }

        Place(panel, editor.Home);
        Clamp(ref editor, item);
        RunActions(panel, ref editor, item);
        item = editor.Host.Item ?? item;
        Clamp(ref editor, item);
        SyncFields(panel, ref editor, item);
        Drag(ref editor, item, input, pressedNow);
        Draw(ref editor, item);
        SyncLabels(ref editor, item);
    }

    private static void Clamp(ref XifEditor editor, Xif item)
    {
        editor.Primitive = Math.Clamp(editor.Primitive, 0, Math.Max(0, item.Primitives.Count - 1));
        int points = Selected(editor, item)?.Points.Count ?? 0;
        editor.Point = Math.Clamp(editor.Point, 0, Math.Max(0, points - 1));
    }

    private static XifPrimitive? Selected(in XifEditor editor, Xif item) =>
        editor.Primitive < item.Primitives.Count ? item.Primitives[editor.Primitive] : null;

    // ------------------------------------------------------------- buttons

    private void RunActions(Entity panel, ref XifEditor editor, Xif item)
    {
        var requests = new List<XifEditorActionKind>();
        foreach (Entity button in Query.Store.Query<XifEditorAction>().ToEntityList())
        {
            XifEditorAction action = button.GetComponent<XifEditorAction>();
            if (action.Editor.Id != panel.Id)
                continue;

            bool pressed = button.Tags.Has<Pressed>();
            if (pressed && _held.Add(button.Id))
                requests.Add(action.Kind);
            else if (!pressed)
                _held.Remove(button.Id);
        }

        foreach (XifEditorActionKind kind in requests)
            Run(ref editor, editor.Host.Item ?? item, kind);
    }

    /// <summary>Applies one button; also how tests and hosts drive the editor without a pointer.</summary>
    public static void Run(ref XifEditor editor, Xif item, XifEditorActionKind kind)
    {
        int primitive = editor.Primitive;
        int point = editor.Point;
        XifPrimitive? selected = Selected(editor, item);
        switch (kind)
        {
            case XifEditorActionKind.PreviousPrimitive:
                if (item.Primitives.Count > 0)
                    editor.Primitive = (primitive + item.Primitives.Count - 1) % item.Primitives.Count;
                editor.Point = 0;
                editor.View = default;
                break;
            case XifEditorActionKind.NextPrimitive:
                if (item.Primitives.Count > 0)
                    editor.Primitive = (primitive + 1) % item.Primitives.Count;
                editor.Point = 0;
                editor.View = default;
                break;
            case XifEditorActionKind.AddPolygon:
                int added = -1;
                editor.Host.Edit(() => added = XifEditing.AddPolygon(item));
                editor.Primitive = added;
                editor.Point = 0;
                editor.View = default;
                editor.Status = "POLYGON ADDED";
                break;
            case XifEditorActionKind.RemovePrimitive:
                if (item.Primitives.Count <= 1)
                {
                    editor.Status = "AN ITEM KEEPS ONE PRIMITIVE";
                    break;
                }
                editor.Host.Edit(() => XifEditing.RemovePrimitive(item, primitive));
                editor.Primitive = Math.Max(0, primitive - 1);
                editor.View = default;
                editor.Status = "PRIMITIVE DELETED";
                break;
            case XifEditorActionKind.InsertPoint:
                if (selected is not { IsPolygon: true })
                {
                    editor.Status = "ONLY POLYGONS HAVE POINTS";
                    break;
                }
                int inserted = -1;
                editor.Host.Edit(() => inserted = XifEditing.InsertPoint(selected, point));
                editor.Point = Math.Max(0, inserted);
                editor.Status = "POINT INSERTED";
                break;
            case XifEditorActionKind.RemovePoint:
                if (selected is null || selected.Points.Count <= XifEditing.MinPolygonPoints)
                {
                    editor.Status = $"A POLYGON KEEPS {XifEditing.MinPolygonPoints} POINTS";
                    break;
                }
                editor.Host.Edit(() => XifEditing.RemovePoint(selected, point));
                editor.Point = Math.Max(0, point - 1);
                editor.Status = "POINT DELETED";
                break;
            case XifEditorActionKind.NextShader:
                if (selected is null || editor.Host.Shaders.Count == 0)
                    break;
                int current = ShaderIndex(editor.Host.Shaders, selected.Shader);
                XifShaderChoice next = editor.Host.Shaders[(current + 1) % editor.Host.Shaders.Count];
                editor.Host.Edit(() => XifEditing.SetShader(selected, next.Shader));
                editor.Status = $"SHADER {next.Label}";
                break;
        }
    }

    private static int ShaderIndex(IReadOnlyList<XifShaderChoice> choices, XifShaderRef shader)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            XifShaderRef choice = choices[i].Shader;
            if (choice.Scope == shader.Scope && choice.Slot == shader.Slot && choice.Path == shader.Path)
                return i;
        }
        return -1;
    }

    // -------------------------------------------------------------- fields

    private void SyncFields(Entity panel, ref XifEditor editor, Xif item)
    {
        foreach (Entity field in Query.Store.Query<XifEditorField>().ToEntityList())
        {
            ref XifEditorField binding = ref field.GetComponent<XifEditorField>();
            if (binding.Editor.Id != panel.Id || !field.HasComponent<TextField>())
                continue;

            bool focused = field.Tags.Has<TextFieldFocused>();
            if (!focused && binding.WasFocused)
                Commit(ref editor, item, binding.Kind, field.GetComponent<TextField>().Value);
            binding.WasFocused = focused;

            if (!focused)
                SetField(field, Format(editor, editor.Host.Item ?? item, binding.Kind));
        }
    }

    /// <summary>Applies a typed value; one that cannot be read is refused and the field shows the item again.</summary>
    public static void Commit(ref XifEditor editor, Xif item, XifEditorFieldKind kind, string typed)
    {
        XifPrimitive? selected = Selected(editor, item);
        switch (kind)
        {
            case XifEditorFieldKind.Name:
                if (typed.Trim() != item.Name)
                {
                    editor.Host.Edit(() => XifEditing.Rename(item, typed));
                    editor.Status = "RENAMED";
                }
                break;
            case XifEditorFieldKind.Fill:
                if (selected is null)
                    break;
                if (!XifEditing.TryParseFill(typed, out Color fill))
                {
                    editor.Status = "FILL IS #RRGGBB OR #RRGGBBAA";
                    break;
                }
                if (fill != selected.Fill)
                {
                    editor.Host.Edit(() => XifEditing.SetFill(selected, fill));
                    editor.Status = "FILL SET";
                }
                break;
            case XifEditorFieldKind.Texture:
                if (selected is null || typed.Trim() == selected.Ref)
                    break;
                editor.Host.Edit(() => XifEditing.SetTexture(selected, typed));
                editor.Status = selected.Ref.Length == 0
                    ? "TEXTURE CLEARED"
                    : editor.Host.CanLoad(selected.Ref) ? "TEXTURE SET" : "TEXTURE SET, BUT IT DOES NOT LOAD";
                break;
        }
    }

    private static string Format(in XifEditor editor, Xif item, XifEditorFieldKind kind)
    {
        XifPrimitive? selected = Selected(editor, item);
        return kind switch
        {
            XifEditorFieldKind.Name => item.Name,
            XifEditorFieldKind.Fill => selected is null ? "" : XifEditing.FormatFill(selected.Fill),
            _ => selected?.Ref ?? "",
        };
    }

    // ------------------------------------------------------------- dragging

    private static void Drag(ref XifEditor editor, Xif item, PointerInput input, bool pressedNow)
    {
        Rect2 box = Area(editor.Parts.Canvas);
        XifPrimitive? selected = Selected(editor, item);
        if (!editor.Dragging && (editor.View.Size == Vector2.Zero))
            editor.View = XifEditing.Bounds(item);
        Transform2D toScreen = XifEditorView.ToScreen(editor.View, box);

        if (editor.Dragging)
        {
            if (!input.Pressed || selected is null)
            {
                editor.Dragging = false;
                editor.Host.EndGesture();
                editor.View = XifEditing.Bounds(item);
                return;
            }

            Vector2 local = toScreen.AffineInverse() * input.Position;
            if (XifEditing.MovePoint(selected, editor.Point, Snap(local, input.Shift)))
                editor.Host.Changed();
            return;
        }

        if (!pressedNow || selected is null || !box.HasPoint(input.Position))
            return;

        int nearest = -1;
        float best = GrabRadius * GrabRadius;
        for (int i = 0; i < selected.Points.Count; i++)
        {
            float distance = (toScreen * selected.Points[i]).DistanceSquaredTo(input.Position);
            if (distance > best)
                continue;
            best = distance;
            nearest = i;
        }

        if (nearest < 0)
            return;

        editor.Point = nearest;
        editor.Dragging = true;
        editor.Host.BeginGesture();
        editor.Status = "DRAG TO MOVE, SHIFT FOR FREE";
    }

    /// <summary>Whole units unless Shift is held, so outlines stay tidy.</summary>
    private static Vector2 Snap(Vector2 point, bool free) => free ? point : point.Round();

    // -------------------------------------------------------------- drawing

    private void Draw(ref XifEditor editor, Xif item)
    {
        Rect2 box = Area(editor.Parts.Canvas);
        Transform2D toScreen = XifEditorView.ToScreen(editor.View, box);
        XifPrimitive? selected = Selected(editor, item);

        var others = new List<Vector2>();
        for (int i = 0; i < item.Primitives.Count; i++)
        {
            if (i == editor.Primitive || item.Primitives[i].Points.Count < 2)
                continue;
            List<Vector2> points = item.Primitives[i].Points;
            foreach (Vector2 point in points)
                others.Add(toScreen * point);
            others.Add(toScreen * points[0]);
        }
        SetShape(editor.Parts.Others, others, closed: false);

        var outline = new List<Vector2>();
        if (selected is not null)
        {
            foreach (Vector2 point in selected.Points)
                outline.Add(toScreen * point);
        }
        SetShape(editor.Parts.Shape, outline, closed: true);
        if (selected is not null && editor.Parts.Shape.HasComponent<UiShape>())
        {
            ref UiShape shape = ref editor.Parts.Shape.GetComponent<UiShape>();
            var fill = new Color(selected.Fill, selected.Fill.A * 0.6f);
            if (shape.Fill != fill)
            {
                shape.Fill = fill;
                editor.Parts.Shape.AddTag<Dirty>();
            }
        }

        Vector2 origin = toScreen * Vector2.Zero;
        Place(editor.Parts.Origin, box.HasPoint(origin) ? origin : UiLayout.Hidden);
        DrawHandles(ref editor, selected, toScreen);
    }

    private void DrawHandles(ref XifEditor editor, XifPrimitive? selected, Transform2D toScreen)
    {
        int count = selected?.Points.Count ?? 0;
        if (editor.Handles.Count != count)
        {
            foreach (Entity handle in editor.Handles)
                UiLayout.DeleteSubtree(handle);
            editor.Handles.Clear();
            UiNode layer = UiNode.Wrap(editor.Parts.Canvas, visuals).LayerNode;
            for (int i = 0; i < count; i++)
            {
                editor.Handles.Add(layer.Rect(Handle, size: new Vector2(HandleSize, HandleSize))
                    .IgnoreLayout().Tag<UiOverlayItem>().At(UiLayout.Hidden));
            }
        }

        for (int i = 0; i < count; i++)
        {
            Entity handle = editor.Handles[i];
            Place(handle, toScreen * selected!.Points[i]);
            Color color = i == editor.Point ? SelectedHandle : Handle;
            if (handle.TryGetComponent(out FillColor fill) && fill.Value != color)
            {
                handle.GetComponent<FillColor>().Value = color;
                handle.AddTag<Dirty>();
            }
        }
    }

    private void HideOutlines(ref XifEditor editor)
    {
        Place(editor.Parts.Others, UiLayout.Hidden);
        Place(editor.Parts.Shape, UiLayout.Hidden);
        Place(editor.Parts.Origin, UiLayout.Hidden);
        foreach (Entity handle in editor.Handles)
            Place(handle, UiLayout.Hidden);
    }

    /// <summary>
    /// Points the floating outline at screen points. The shape sits at the
    /// origin of its layer, so its points are the screen points themselves.
    /// </summary>
    private static void SetShape(Entity entity, List<Vector2> points, bool closed)
    {
        if (entity.IsNull || !entity.HasComponent<UiShape>())
            return;

        if (points.Count < 2)
        {
            Place(entity, UiLayout.Hidden);
            return;
        }

        Place(entity, Vector2.Zero);
        ref UiShape shape = ref entity.GetComponent<UiShape>();
        shape.Points = points.ToArray();
        shape.Closed = closed;
        entity.AddTag<Dirty>();
    }

    // ------------------------------------------------------------- readouts

    private static void SyncLabels(ref XifEditor editor, Xif item)
    {
        XifPrimitive? selected = Selected(editor, item);
        SetText(editor.Parts.Title, editor.Host.Title.ToUpperInvariant());
        SetText(editor.Parts.PrimitiveLabel, selected is null
            ? "NO PRIMITIVES"
            : $"{selected.Kind.ToUpperInvariant()} {editor.Primitive + 1}/{item.Primitives.Count}");
        SetText(editor.Parts.PointLabel, selected is { Points.Count: > 0 }
            ? $"PT {editor.Point + 1}/{selected.Points.Count}  {selected.Points[editor.Point].X:0.#}, {selected.Points[editor.Point].Y:0.#}"
            : "");

        string shader = "NONE";
        if (selected is not null)
        {
            int index = ShaderIndex(editor.Host.Shaders, selected.Shader);
            shader = index >= 0 ? editor.Host.Shaders[index].Label : selected.Shader.IsNone ? "NONE" : "NOT ALLOWED HERE";
        }
        SetText(editor.Parts.Shader, shader);
        SetText(editor.Parts.Status, editor.Status ?? "");
    }

    // --------------------------------------------------------------- helpers

    /// <summary>An entity's box on screen.</summary>
    public static Rect2 Area(Entity entity)
    {
        if (entity.IsNull || !entity.HasComponent<Rectangle>() || !entity.HasComponent<Position>())
            return new Rect2();

        Vector2 center = UiTransform.WorldPosition(entity, entity.GetComponent<Position>().Value);
        Vector2 size = entity.GetComponent<Rectangle>().Bounds.Size;
        return new Rect2(center - size / 2, size);
    }

    private static void Place(Entity entity, Vector2 position)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        ref Position current = ref entity.GetComponent<Position>();
        if (current.Value == position)
            return;

        current.Value = position;
        entity.AddTag<Moved>();
        entity.AddTag<Dirty>();
    }

    private static void SetText(Entity entity, string value)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        entity.AddTag<Dirty>();
    }

    private static void SetField(Entity entity, string value)
    {
        ref TextField field = ref entity.GetComponent<TextField>();
        if (field.Value != value)
            field.Value = value;
        SetText(entity, value);
    }
}
