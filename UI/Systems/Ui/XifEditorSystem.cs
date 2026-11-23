using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Items;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>
/// Runs every XIF editor panel: shows it while it is open and its host has an
/// item, applies its buttons, fields and colour picker, and edits the item by
/// hand in its shape box: drag a point, click an edge to add a point there,
/// right-click a point to delete it, click a shape to pick it and drag it to
/// move it. Every edit goes through the host, which records it however its
/// tool does undo; a drag or a picker drag is one step.
/// </summary>
public sealed class XifEditorSystem : QuerySystem<XifEditor>
{
    private readonly IPointerInputSource _pointer;
    private readonly IVisualItemFactory? _visuals;

    public XifEditorSystem(IPointerInputSource pointer, IVisualItemFactory? visuals = null)
    {
        _pointer = pointer;
        _visuals = visuals;

        // A closed panel is hidden, and still has to notice being opened.
        Filter.WithDisabled();
    }

    /// <summary>A press this close to a point, in screen pixels, grabs it.</summary>
    public const float GrabRadius = 10f;

    /// <summary>A press this close to an edge, in screen pixels, adds a point there.</summary>
    public const float EdgeRadius = 7f;

    /// <summary>How much room the view leaves round the item, as a share of its bigger side.</summary>
    private const float ViewMargin = 0.3f;

    private const float HandleSize = 9f;
    private const float HoverHandleSize = 13f;
    private static readonly Color Handle = Palette.Accent;
    private static readonly Color HoverHandle = new("#fde68a");
    private static readonly Color SelectedHandle = Colors.White;

    private readonly ButtonPress _presses = new();
    private bool _wasPressed;
    private bool _wasSecondary;

    /// <summary>Opens the editor over the host's work, on whatever the host has now.</summary>
    public static void Open(Entity panel)
    {
        if (panel.IsNull || !panel.HasComponent<XifEditor>())
            return;
        ref XifEditor editor = ref panel.GetComponent<XifEditor>();
        editor.Open = true;
        editor.Polygon = 0;
        editor.Point = 0;
        editor.View = default;
        editor.Status = "DRAG POINTS · CLICK AN EDGE TO ADD ONE · RIGHT-CLICK ONE TO DELETE IT";
    }

    public static void Close(Entity panel)
    {
        if (!panel.IsNull && panel.HasComponent<XifEditor>())
            panel.GetComponent<XifEditor>().Open = false;
    }

    public static bool IsOpen(Entity panel) =>
        !panel.IsNull && panel.HasComponent<XifEditor>() && panel.GetComponent<XifEditor>().Open;

    protected override void OnUpdate()
    {
        PointerInput input = _pointer.Read();
        bool pressedNow = input.Pressed && !_wasPressed;
        bool secondaryNow = input.SecondaryPressed && !_wasSecondary;
        _wasPressed = input.Pressed;
        _wasSecondary = input.SecondaryPressed;

        foreach (Entity panel in Query.ToEntityList())
            Update(panel, input, pressedNow, secondaryNow);
    }

    private void Update(Entity panel, PointerInput input, bool pressedNow, bool secondaryNow)
    {
        ref XifEditor editor = ref panel.GetComponent<XifEditor>();
        Xif? item = editor.Open ? editor.Host.Item : null;
        if (item is null)
        {
            EndGestures(ref editor);
            editor.Open = false;
            HideShapes(ref editor);
            UiLayout.SetHidden(editor.Parts.Backdrop, true);
            UiLayout.SetHidden(panel, true);
            return;
        }

        float opacity = Math.Clamp(editor.Host.Opacity, 0f, 1f);
        Fade(editor.Parts.Backdrop, XifEditorPanel.BackdropColor, opacity);
        Fade(editor.Parts.Canvas, XifEditorPanel.CanvasColor, opacity);
        Place(editor.Parts.Backdrop, editor.Parts.Screen / 2f);
        UiLayout.SetHidden(editor.Parts.Backdrop, false);
        Place(panel, editor.Home);
        UiLayout.SetHidden(panel, false);
        editor = ref panel.GetComponent<XifEditor>();
        Clamp(ref editor, item);
        RunActions(panel, ref editor, item);
        if (!editor.Open)
            return;
        item = editor.Host.Item ?? item;
        Clamp(ref editor, item);
        SyncFields(panel, ref editor, item);
        SyncShapeList(ref editor, item);
        SyncColour(ref editor, item);
        SyncLayers(ref editor, item);
        SyncSettings(ref editor, item);
        Edit(ref editor, item, input, pressedNow, secondaryNow);
        Draw(ref editor, item, input);
        SyncLabels(ref editor, item);
    }

    private static void EndGestures(ref XifEditor editor)
    {
        if (editor.Dragging)
            editor.Host.EndGesture();
        if (editor.Colouring)
            editor.Host.EndGesture();
        editor.Drag = XifEditorDrag.None;
        editor.Colouring = false;
    }

    private static void Clamp(ref XifEditor editor, Xif item)
    {
        editor.Polygon = Math.Clamp(editor.Polygon, 0, Math.Max(0, item.Polygons.Count - 1));
        int points = Selected(editor, item) is { } shape ? XifEditing.Handles(shape).Count : 0;
        editor.Point = Math.Clamp(editor.Point, 0, Math.Max(0, points - 1));
    }

    private static XifPolygon? Selected(in XifEditor editor, Xif item) =>
        editor.Polygon < item.Polygons.Count ? item.Polygons[editor.Polygon] : null;

    // ------------------------------------------------------------- buttons

    private void RunActions(Entity panel, ref XifEditor editor, Xif item)
    {
        var requests = new List<(XifEditorActionKind Kind, int Arg)>();
        foreach ((Entity button, XifEditorActionKind kind, UiCommand command) in UiCommand.All<XifEditorActionKind>(Query.Store, panel))
        {
            if (_presses.Started(button))
                requests.Add((kind, command.Arg));
        }

        foreach ((XifEditorActionKind kind, int arg) in requests)
            Run(ref editor, editor.Host.Item ?? item, kind, arg);
    }

    /// <summary>Applies one button; also how tests and hosts drive the editor without a pointer.</summary>
    public static void Run(ref XifEditor editor, Xif item, XifEditorActionKind kind, int arg = 0)
    {
        int polygon = editor.Polygon;
        int point = editor.Point;
        XifPolygon? selected = Selected(editor, item);
        switch (kind)
        {
            case XifEditorActionKind.PickPolygon:
                if (arg >= 0 && arg < item.Polygons.Count)
                {
                    editor.Polygon = arg;
                    editor.Point = 0;
                }
                break;
            case XifEditorActionKind.ToggleFill:
                if (arg < 0 || arg >= item.Polygons.Count)
                    break;
                editor.FillOpen = !(editor.FillOpen && editor.Polygon == arg);
                editor.Polygon = arg;
                editor.Point = 0;
                break;
            case XifEditorActionKind.PreviousPolygon:
                if (item.Polygons.Count > 0)
                    editor.Polygon = (polygon + item.Polygons.Count - 1) % item.Polygons.Count;
                editor.Point = 0;
                break;
            case XifEditorActionKind.NextPolygon:
                if (item.Polygons.Count > 0)
                    editor.Polygon = (polygon + 1) % item.Polygons.Count;
                editor.Point = 0;
                break;
            case XifEditorActionKind.AddPolygon:
                int added = -1;
                editor.Host.Edit(() => added = XifEditing.AddPolygon(item));
                editor.Polygon = added;
                editor.Point = 0;
                editor.View = default;
                editor.Status = "SHAPE ADDED · DRAG ITS POINTS";
                break;
            case XifEditorActionKind.AddQuad or XifEditorActionKind.AddCircle or XifEditorActionKind.AddCurve:
                XifShapeKind shapeKind = kind switch
                {
                    XifEditorActionKind.AddQuad => XifShapeKind.Quad,
                    XifEditorActionKind.AddCurve => XifShapeKind.Curve,
                    _ => XifShapeKind.Ellipse,
                };
                int primitive = -1;
                editor.Host.Edit(() => primitive = XifEditing.AddPrimitive(item, shapeKind));
                editor.Polygon = primitive;
                editor.Point = 0;
                editor.View = default;
                editor.Status = shapeKind == XifShapeKind.Curve
                    ? "CURVE ADDED · DRAG ITS POINTS AND THE CONTROLS BETWEEN THEM"
                    : $"{KindName(shapeKind)} ADDED · DRAG ITS HANDLES TO SIZE IT";
                break;
            case XifEditorActionKind.ToggleFit:
                bool fit = !(item.Get<XifShader>()?.Fit ?? false);
                editor.Host.Edit(() => XifEditing.SetFit(item, fit));
                editor.Status = fit ? "SHADER FITTED TO THE SHAPE" : "SHADER SIZED BY ITS SETTINGS";
                break;
            case XifEditorActionKind.RemovePolygon:
                if (item.Polygons.Count <= 1)
                {
                    editor.Status = "AN ITEM KEEPS ONE SHAPE";
                    break;
                }
                editor.Host.Edit(() => XifEditing.RemovePolygon(item, polygon));
                editor.Polygon = Math.Max(0, polygon - 1);
                editor.View = default;
                editor.Status = "SHAPE DELETED";
                break;
            case XifEditorActionKind.InsertPoint:
                if (selected is null)
                    break;
                if (selected.Kind is not (XifShapeKind.Free or XifShapeKind.Curve))
                {
                    editor.Status = $"A {KindName(selected.Kind)} KEEPS ITS SHAPE · DRAG ITS HANDLES";
                    break;
                }
                int inserted = -1;
                editor.Host.Edit(() => inserted = XifEditing.InsertPoint(selected, point));
                editor.Point = Math.Max(0, inserted);
                editor.Status = "POINT ADDED";
                break;
            case XifEditorActionKind.RemovePoint:
                RemovePoint(ref editor, selected, point);
                break;
            case XifEditorActionKind.NextShader:
                if (editor.Host.Shaders.Count == 0)
                    break;
                int current = ShaderIndex(editor.Host.Shaders, item.ShaderRef);
                XifShaderChoice next = editor.Host.Shaders[(current + 1) % editor.Host.Shaders.Count];
                editor.Host.Edit(() => XifEditing.SetShader(item, next.Shader));
                editor.Status = $"SHADER {next.Label}";
                break;
            case XifEditorActionKind.RemoveLayer:
                if (item.Get<XifShader>()?.Layers is { } layers && arg >= 0 && arg < layers.Count)
                {
                    string gone = LayerLabel(editor.Host, layers[arg]);
                    editor.Host.Edit(() => XifEditing.RemoveLayer(item, arg));
                    editor.Status = $"LAYER {gone} REMOVED";
                }
                break;
            case XifEditorActionKind.LayerUp or XifEditorActionKind.LayerDown:
                int step = kind == XifEditorActionKind.LayerUp ? 1 : -1;
                bool moved = false;
                editor.Host.Edit(() => moved = XifEditing.MoveLayer(item, arg, step));
                editor.Status = moved ? "LAYER MOVED" : step > 0 ? "ALREADY ON TOP" : "ALREADY AT THE BOTTOM";
                break;
            case XifEditorActionKind.Done:
                EndGestures(ref editor);
                editor.Open = false;
                break;
        }
    }

    private static void RemovePoint(ref XifEditor editor, XifPolygon? polygon, int point)
    {
        if (polygon is { Kind: not (XifShapeKind.Free or XifShapeKind.Curve) })
        {
            editor.Status = $"A {KindName(polygon.Kind)} KEEPS ITS SHAPE · DRAG ITS HANDLES";
            return;
        }
        if (polygon is { Kind: XifShapeKind.Curve } && polygon.Points.Count <= XifEditing.MinCurvePoints)
        {
            editor.Status = "A CURVE KEEPS TWO SEGMENTS";
            return;
        }
        if (polygon is null || polygon.Points.Count <= XifEditing.MinPolygonPoints)
        {
            editor.Status = $"A SHAPE KEEPS {XifEditing.MinPolygonPoints} POINTS";
            return;
        }
        editor.Host.Edit(() => XifEditing.RemovePoint(polygon, point));
        editor.Point = Math.Max(0, point - 1);
        editor.Status = "POINT DELETED";
    }

    /// <summary>
    /// One row per stacked layer, top first: its name, then up, down and
    /// remove. Rebuilt when the stack changes. A pick in the add list stacks
    /// that layer on top (one undo step) and the list goes back to reading + LAYER.
    /// </summary>
    private void SyncLayers(ref XifEditor editor, Xif item)
    {
        Entity list = editor.Parts.LayerList;
        if (list.IsNull)
            return;

        List<string> layers = item.Get<XifShader>()?.Layers ?? [];
        string key = string.Join("|", layers);
        if (key != editor.LayersFor)
        {
            editor.LayersFor = key;
            UiLayout.DeleteChildren(list);
            Entity panel = FindPanel(list);
            UiNode column = UiNode.Wrap(list, _visuals);
            const float small = 26;
            float name = XifEditorPanel.SideWidth - small * 3 - 12;
            // Top of the stack first, as it draws: up moves a row up the list.
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                UiNode row = column.Row().Gap(4);
                row.Text(LayerLabel(editor.Host, layers[i]), UiType.Caption, UiTheme.Text)
                    .Size(name, XifEditorPanel.ShapeRowHeight);
                row.Button("▲", UiTheme.SmallFontSize).Size(small, XifEditorPanel.ShapeRowHeight)
                    .With(UiCommand.Of(XifEditorActionKind.LayerUp, arg: i, owner: panel))
                    .Tooltip("Move this layer up: it works on what the one above it gave");
                row.Button("▼", UiTheme.SmallFontSize).Size(small, XifEditorPanel.ShapeRowHeight)
                    .With(UiCommand.Of(XifEditorActionKind.LayerDown, arg: i, owner: panel))
                    .Tooltip("Move this layer down the stack");
                row.Button("×", UiTheme.SmallFontSize).Size(small, XifEditorPanel.ShapeRowHeight)
                    .With(UiCommand.Of(XifEditorActionKind.RemoveLayer, arg: i, owner: panel))
                    .Tooltip("Take this layer off the stack");
            }
        }
    }

    /// <summary>
    /// A pick in the + LAYER list (<see cref="UiDropdowns.OnPicked"/> on
    /// <see cref="XifEditorParts.LayerAdd"/>): stacks that shader over the open item.
    /// </summary>
    public static void AddLayer(Entity panel, int pick)
    {
        if (panel.IsNull || !panel.HasComponent<XifEditor>())
            return;

        ref XifEditor editor = ref panel.GetComponent<XifEditor>();
        Xif? item = editor.Open ? editor.Host.Item : null;
        IReadOnlyList<XifShaderChoice> choices = editor.Host.Layers;
        if (item is null || pick < 0 || pick >= choices.Count)
            return;
        string path = choices[pick].Shader.Path;
        string chosen = choices[pick].Label;
        bool added = false;
        editor.Host.Edit(() => added = XifEditing.AddLayer(item, path));
        editor = ref panel.GetComponent<XifEditor>();
        editor.Status = added ? $"LAYER {chosen} ADDED" : $"{chosen} IS ALREADY IN THE STACK";
        Entity add = editor.Parts.LayerAdd;
        if (!add.TryGetComponent(out Text shown) || shown.Value != XifEditorPanel.AddLayerText)
            UiText.Set(add, XifEditorPanel.AddLayerText);
    }

    /// <summary>What a layer's row calls it: the host's label for it, else its file's name.</summary>
    private static string LayerLabel(IXifEditorHost host, string path)
    {
        foreach (XifShaderChoice choice in host.Layers)
        {
            if (choice.Shader.Path == path)
                return choice.Label;
        }
        return System.IO.Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
    }

    private static int ShaderIndex(IReadOnlyList<XifShaderChoice> choices, XifShaderRef shader)
    {
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i].Shader.Same(shader))
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
                SetField(field, Format(editor.Host.Item ?? item, binding.Kind));
        }
    }

    /// <summary>Applies a typed value; one that cannot be read is refused and the field shows the item again.</summary>
    public static void Commit(ref XifEditor editor, Xif item, XifEditorFieldKind kind, string typed)
    {
        if (kind == XifEditorFieldKind.Name && typed.Trim() != item.Name)
        {
            editor.Host.Edit(() => XifEditing.Rename(item, typed));
            editor.Status = "RENAMED";
        }
    }

    private static string Format(Xif item, XifEditorFieldKind kind) =>
        kind == XifEditorFieldKind.Name ? item.Name : "";

    /// <summary>
    /// The picked shape's colour: a picker drag recolours it live inside one
    /// gesture, a typed or dropped colour is one edit, and otherwise the field
    /// shows the shape's colour.
    /// </summary>
    private static void SyncColour(ref XifEditor editor, Xif item)
    {
        Entity field = editor.Parts.Fill;
        if (field.IsNull || !field.HasComponent<UiColorField>())
            return;

        XifPolygon? selected = Selected(editor, item);
        bool editing = UiColorField.IsEditing(field);
        if (UiColorField.TakeLive(field, out Color live) && selected is not null)
        {
            if (!editor.Colouring)
            {
                editor.Host.BeginGesture();
                editor.Colouring = true;
            }
            selected.Fill = live;
            editor.Host.Changed();
        }

        if (UiColorField.TakeCommit(field, out Color colour) && selected is not null)
        {
            if (editor.Colouring)
            {
                selected.Fill = colour;
                editor.Host.EndGesture();
                editor.Colouring = false;
            }
            else
            {
                editor.Host.Edit(() => XifEditing.SetFill(selected, colour));
            }
            editor.Status = "COLOUR SET";
        }

        // A picker drag that ends where it began commits nothing, and its gesture closes empty.
        if (editor.Colouring && !editing)
        {
            editor.Host.EndGesture();
            editor.Colouring = false;
        }

        UiColorField.Show(field, selected?.Fill ?? Colors.White);
    }

    // ---------------------------------------------------------- shape list

    /// <summary>
    /// One row per shape: a swatch in its colour (click to open the picker for
    /// it) and its name (click to pick it), the picked one lit. Rebuilt when
    /// the count changes; the picker shows under the list while open.
    /// </summary>
    private void SyncShapeList(ref XifEditor editor, Xif item)
    {
        Entity list = editor.Parts.ShapeList;
        if (list.IsNull)
            return;

        if (editor.ShapeRows.Count != item.Polygons.Count)
        {
            UiLayout.DeleteChildren(list);
            editor.ShapeRows.Clear();
            Entity panel = FindPanel(list);
            UiNode column = UiNode.Wrap(list, _visuals);
            float name = XifEditorPanel.SideWidth - XifEditorPanel.SwatchWidth - 4;
            for (int i = 0; i < item.Polygons.Count; i++)
            {
                UiNode row = column.Row().Gap(4);
                Entity swatch = row.Button("", UiTheme.SmallFontSize)
                    .Size(XifEditorPanel.SwatchWidth, XifEditorPanel.ShapeRowHeight)
                    .With(UiCommand.Of(XifEditorActionKind.ToggleFill, arg: i, owner: panel))
                    .Tooltip("This shape's colour: click to open the picker for it").Entity;
                Entity label = row.Button("", UiTheme.SmallFontSize)
                    .Size(name, XifEditorPanel.ShapeRowHeight)
                    .With(UiCommand.Of(XifEditorActionKind.PickPolygon, arg: i, owner: panel))
                    .Tooltip("Pick this shape: its points show in the box").Entity;
                label.AddComponent(new TextAlignment(TextAlign.Left));
                editor.ShapeRows.Add((swatch, label));
            }
        }

        for (int i = 0; i < editor.ShapeRows.Count; i++)
        {
            (Entity swatch, Entity label) = editor.ShapeRows[i];
            Paint(swatch, item.Polygons[i].Fill);
            UiText.Set(label, $"{KindName(item.Polygons[i].Kind)} {i + 1}");
            Paint(label, i == editor.Polygon ? Palette.Primary : UiTheme.Button);
        }

        UiLayout.SetHidden(editor.Parts.FillBox, !editor.FillOpen || item.Polygons.Count == 0);
    }

    /// <summary>What a shape's row and messages call its kind.</summary>
    private static string KindName(XifShapeKind kind) => kind switch
    {
        XifShapeKind.Quad => "QUAD",
        XifShapeKind.Ellipse => "CIRCLE",
        XifShapeKind.Curve => "CURVE",
        _ => "SHAPE",
    };

    /// <summary>The panel that owns a part, so a row's command reaches this editor.</summary>
    private static Entity FindPanel(Entity part)
    {
        for (Entity at = part; !at.IsNull; at = at.Parent)
        {
            if (at.HasComponent<XifEditor>())
                return at;
        }
        return default;
    }

    private static void Paint(Entity button, Color colour)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;
        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == colour)
            return;
        visual.Normal = colour;
        visual.Hover = colour.Lerp(Colors.White, 0.16f);
        button.AddTag<Dirty>();
    }

    // ------------------------------------------------------------ settings

    /// <summary>
    /// The shader's settings as value fields under SHADER: rebuilt when the
    /// shader changes, kept on the item's values, and each edit written into
    /// the item's params through the host (a scrub is one undo step). A value
    /// the item never set shows the shader's own default.
    /// </summary>
    private void SyncSettings(ref XifEditor editor, Xif item)
    {
        if (editor.Parts.Settings.IsNull)
            return;

        XifShaderRef shader = item.ShaderRef;
        string key = $"{shader.Scope}:{shader.Path}:{shader.Slot}:{XifShaderParams.StackKey(item)}";
        if (key != editor.SettingsFor)
            BuildSettings(ref editor, item, key);

        bool fitted = item.Get<XifShader>()?.Fit ?? false;
        UiText.Set(editor.Parts.FitToggle, fitted ? "FIT TO SHAPE: ON" : "FIT TO SHAPE: OFF");
        Paint(editor.Parts.FitToggle, fitted ? Palette.Primary : UiTheme.Button);

        foreach (XifSettingField setting in editor.SettingFields)
        {
            if (setting.Options is not null)
            {
                SyncChoice(ref editor, item, setting);
                continue;
            }
            if (setting.Field.IsNull || !setting.Field.HasComponent<UiValueField>())
                continue;
            ref UiValueField field = ref setting.Field.GetComponent<UiValueField>();
            bool changed = false;
            for (int i = 0; i < field.Values.Length && i < setting.Shown.Length; i++)
                changed |= field.Values[i] != setting.Shown[i];

            if (changed)
            {
                float[] values = (float[])field.Values.Clone();
                string name = setting.Name;
                if (field.Live)
                {
                    if (!setting.Dragging)
                    {
                        editor.Host.BeginGesture();
                        setting.Dragging = true;
                    }
                    XifEditing.SetParam(item, name, values);
                    editor.Host.Changed();
                }
                else if (setting.Dragging)
                {
                    XifEditing.SetParam(item, name, values);
                    editor.Host.EndGesture();
                    setting.Dragging = false;
                }
                else
                {
                    editor.Host.Edit(() => XifEditing.SetParam(item, name, values));
                }
                editor.Status = $"{Label(name)} SET";
            }
            else if (setting.Dragging && !field.Live)
            {
                editor.Host.EndGesture();
                setting.Dragging = false;
            }

            if (!field.Live && !Typing(field))
            {
                float[] now = item.Get<XifShader>()?.Params.TryGetValue(setting.Name, out float[]? stored) == true
                    ? stored!
                    : field.Defaults;
                for (int i = 0; i < field.Values.Length; i++)
                    field.Values[i] = i < now.Length ? now[i] : field.Defaults[i];
            }
            Array.Copy(field.Values, setting.Shown, Math.Min(field.Values.Length, setting.Shown.Length));
        }
    }

    private void BuildSettings(ref XifEditor editor, Xif item, string key)
    {
        editor.SettingsFor = key;
        foreach (XifSettingField old in editor.SettingFields)
        {
            if (old.Dragging)
                editor.Host.EndGesture();
        }
        editor.SettingFields.Clear();
        UiLayout.DeleteChildren(editor.Parts.Settings);

        IReadOnlyList<MaterialParam> settings = editor.Host.ShaderParams(item) ?? [];
        UiLayout.SetHidden(editor.Parts.SettingsCaption, settings.Count == 0);
        bool fits = false;
        foreach (MaterialParam param in settings)
            fits |= param.Fit;
        UiLayout.SetHidden(editor.Parts.FitToggle, !fits);
        UiNode column = UiNode.Wrap(editor.Parts.Settings, _visuals);
        Dictionary<string, float[]>? stored = item.Get<XifShader>()?.Params;
        foreach (MaterialParam param in settings)
        {
            float[]? initial = stored is not null && stored.TryGetValue(param.Name, out float[]? values) ? values : null;
            UiNode node = column.SettingField(param, initial, XifEditorPanel.SideWidth);
            if (node.Entity.TryGetComponent(out Dropdown choice))
            {
                editor.SettingFields.Add(new XifSettingField(param.Name, node.Entity, [choice.Selected]) { Options = param.Options });
                continue;
            }
            editor.SettingFields.Add(new XifSettingField(param.Name, node.Entity, UiValueFields.Get(node.Entity)));
        }
    }

    /// <summary>
    /// A setting with named choices: a pick in its list sets the value (one
    /// undo step); a value changed elsewhere (undo) moves the list to it.
    /// </summary>
    private static void SyncChoice(ref XifEditor editor, Xif item, XifSettingField setting)
    {
        if (setting.Field.IsNull || !setting.Field.HasComponent<Dropdown>())
            return;

        ref Dropdown dropdown = ref setting.Field.GetComponent<Dropdown>();
        IReadOnlyList<string> options = setting.Options!;
        string name = setting.Name;
        if (dropdown.Selected != (int)setting.Shown[0])
        {
            float[] values = [dropdown.Selected];
            editor.Host.Edit(() => XifEditing.SetParam(item, name, values));
            editor.Status = $"{Label(name)}: {options[dropdown.Selected].ToUpperInvariant()}";
        }
        else if (item.Get<XifShader>()?.Params.TryGetValue(name, out float[]? stored) == true && stored.Length > 0)
        {
            dropdown.Selected = Math.Clamp((int)MathF.Round(stored[0]), 0, options.Count - 1);
        }

        setting.Shown[0] = dropdown.Selected;
    }

    private static string Label(string name) => UiValueFields.SettingLabel(name);

    /// <summary>A slot is being typed in: leave the field alone until it lets go.</summary>
    private static bool Typing(in UiValueField field)
    {
        foreach (Entity slot in field.Slots)
        {
            if (!slot.IsNull && slot.Tags.Has<TextFieldFocused>())
                return true;
        }
        return false;
    }

    // ------------------------------------------------------------- editing

    /// <summary>The item's box with room round it, so points near its edge stay easy to grab.</summary>
    private static Rect2 ViewOf(Xif item)
    {
        Rect2 bounds = XifEditing.Bounds(item);
        float side = MathF.Max(MathF.Max(bounds.Size.X, bounds.Size.Y), 40f);
        return bounds.Grow(side * ViewMargin);
    }

    private static void Edit(ref XifEditor editor, Xif item, PointerInput input, bool pressedNow, bool secondaryNow)
    {
        Rect2 box = Area(editor.Parts.Canvas);
        Transform2D toScreen = ToScreen(ref editor, item, box);
        Transform2D toItem = toScreen.AffineInverse();
        XifPolygon? selected = Selected(editor, item);

        if (editor.Dragging)
        {
            if (!input.Pressed || selected is null)
            {
                editor.Drag = XifEditorDrag.None;
                editor.Host.EndGesture();
                editor.View = ViewOf(item);
                return;
            }

            Vector2 at = Snap(toItem * input.Position, input.Shift);
            if (editor.Drag == XifEditorDrag.Point)
            {
                if (XifEditing.MoveHandle(selected, editor.Point, at))
                    editor.Host.Changed();
            }
            else
            {
                Vector2 by = at - editor.DragFrom;
                if (by != Vector2.Zero)
                {
                    for (int i = 0; i < selected.Points.Count; i++)
                        selected.Points[i] += by;
                    editor.DragFrom = at;
                    editor.Host.Changed();
                }
            }
            return;
        }

        bool inBox = box.HasPoint(input.Position);
        editor.HoverPoint = inBox && selected is not null ? NearestPoint(selected, toScreen, input.Position) : -1;

        if (secondaryNow && inBox && editor.HoverPoint >= 0)
        {
            RemovePoint(ref editor, selected, editor.HoverPoint);
            return;
        }

        if (!pressedNow || !inBox)
            return;

        // A point first, then an edge of the picked shape, then any shape under the pointer.
        if (selected is not null && editor.HoverPoint >= 0)
        {
            editor.Point = editor.HoverPoint;
            editor.Drag = XifEditorDrag.Point;
            editor.Host.BeginGesture();
            editor.Status = "DRAG TO MOVE · SHIFT FOR NO SNAPPING";
            return;
        }

        if (selected is { Kind: XifShapeKind.Free } && NearestEdge(selected, toScreen, input.Position) is { } edge)
        {
            // Adding the point and dragging it on are one step.
            editor.Host.BeginGesture();
            int inserted = XifEditing.InsertPoint(selected, edge.After);
            if (inserted >= 0)
            {
                XifEditing.MovePoint(selected, inserted, Snap(toItem * edge.Screen, input.Shift));
                editor.Point = inserted;
                editor.Drag = XifEditorDrag.Point;
                editor.Host.Changed();
                editor.Status = "POINT ADDED · DRAG TO PLACE IT";
            }
            else
            {
                editor.Host.EndGesture();
            }
            return;
        }

        Vector2 local = toItem * input.Position;
        int picked = ShapeAt(item, local);
        if (picked < 0)
            return;
        editor.Polygon = picked;
        editor.Point = 0;
        editor.Drag = XifEditorDrag.Polygon;
        editor.DragFrom = Snap(local, input.Shift);
        editor.Host.BeginGesture();
        editor.Status = $"SHAPE {picked + 1} · DRAG TO MOVE IT";
    }

    /// <summary>Item space to screen: where the host places the item, or fitted in the shape box.</summary>
    private static Transform2D ToScreen(ref XifEditor editor, Xif item, Rect2 box)
    {
        if (editor.Host.Placement is { } placed)
            return placed;
        if (!editor.Dragging && editor.View.Size == Vector2.Zero)
            editor.View = ViewOf(item);
        return XifEditorView.ToScreen(editor.View, box);
    }

    private static int NearestPoint(XifPolygon polygon, Transform2D toScreen, Vector2 pointer)
    {
        int nearest = -1;
        float best = GrabRadius * GrabRadius;
        List<Vector2> handles = XifEditing.Handles(polygon);
        for (int i = 0; i < handles.Count; i++)
        {
            float distance = (toScreen * handles[i]).DistanceSquaredTo(pointer);
            if (distance > best)
                continue;
            best = distance;
            nearest = i;
        }
        return nearest;
    }

    /// <summary>The edge of <paramref name="polygon"/> under the pointer: the point it starts at, and the spot on it nearest the pointer, on screen.</summary>
    private static (int After, Vector2 Screen)? NearestEdge(XifPolygon polygon, Transform2D toScreen, Vector2 pointer)
    {
        (int, Vector2)? found = null;
        float best = EdgeRadius * EdgeRadius;
        int count = polygon.Points.Count;
        for (int i = 0; i < count && count >= 2; i++)
        {
            Vector2 a = toScreen * polygon.Points[i];
            Vector2 b = toScreen * polygon.Points[(i + 1) % count];
            Vector2 along = b - a;
            float length = along.LengthSquared();
            if (length < 1f)
                continue;
            float t = Math.Clamp((pointer - a).Dot(along) / length, 0f, 1f);
            // Close to either end it is the point's to grab, not the edge's.
            if (t < 0.08f || t > 0.92f)
                continue;
            Vector2 spot = a + along * t;
            float distance = spot.DistanceSquaredTo(pointer);
            if (distance > best)
                continue;
            best = distance;
            found = (i, spot);
        }
        return found;
    }

    /// <summary>The topmost shape holding an item-space point, or -1.</summary>
    private static int ShapeAt(Xif item, Vector2 point)
    {
        for (int i = item.Polygons.Count - 1; i >= 0; i--)
        {
            if (Holds(item.Polygons[i].Points, point))
                return i;
        }
        return -1;
    }

    /// <summary>Even-odd: whether a ray from the point crosses the outline an odd number of times.</summary>
    private static bool Holds(List<Vector2> outline, Vector2 point)
    {
        bool inside = false;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            Vector2 a = outline[i], b = outline[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) &&
                point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Whole units unless Shift is held, so outlines stay tidy.</summary>
    private static Vector2 Snap(Vector2 point, bool free) => free ? point : point.Round();

    // -------------------------------------------------------------- drawing

    private void Draw(ref XifEditor editor, Xif item, PointerInput input)
    {
        Rect2 box = Area(editor.Parts.Canvas);
        Transform2D toScreen = ToScreen(ref editor, item, box);
        editor.Shown = toScreen;
        editor.ShownBox = box;
        XifPolygon? selected = Selected(editor, item);

        // Every shape filled in its own colour, back to front, as the item looks.
        // Placed by the host, or drawn by it in the box, the item is the host's
        // own drawing, through its shader: plain fills on top would hide what
        // it really looks like, so only the field lines and points draw over it.
        bool placed = editor.Host.Placement is not null || editor.Host.DrawsItem;
        List<Entity> fills = editor.Parts.Fills;
        for (int i = 0; i < fills.Count; i++)
        {
            // A curve's points are a control cage, not an outline: only the host's drawing shows it filled.
            if (placed || i >= item.Polygons.Count || item.Polygons[i].Points.Count < 3 || item.Polygons[i].Kind == XifShapeKind.Curve)
            {
                UiLayout.SetHidden(fills[i], true);
                continue;
            }
            var points = new List<Vector2>(item.Polygons[i].Points.Count);
            foreach (Vector2 point in item.Polygons[i].Points)
                points.Add(toScreen * point);
            SetShape(fills[i], points, closed: true);
            ref UiShape shape = ref fills[i].GetComponent<UiShape>();
            Color stroke = i == editor.Polygon ? new Color(0, 0, 0, 0) : new Color(UiTheme.TextDim, 0.35f);
            // Always at full strength, see-through or not: the shapes are what is being edited.
            Color fill = item.Polygons[i].Fill;
            if (shape.Fill != fill || shape.Stroke != stroke)
            {
                shape.Fill = fill;
                shape.Stroke = stroke;
                fills[i].AddTag<Dirty>();
            }
        }

        var outline = new List<Vector2>();
        if (selected is not null)
        {
            foreach (Vector2 point in selected.Points)
                outline.Add(toScreen * point);
        }
        SetShape(editor.Parts.Shape, outline, closed: true);

        Vector2 origin = toScreen * Vector2.Zero;
        Place(editor.Parts.Origin, origin);
        UiLayout.SetHidden(editor.Parts.Origin, !box.HasPoint(origin));

        // Where a click would add a point: on the picked shape's edge under the pointer.
        bool ghost = !editor.Dragging && selected is { Kind: XifShapeKind.Free } && editor.HoverPoint < 0 && box.HasPoint(input.Position) &&
                     NearestEdge(selected, toScreen, input.Position) is not null;
        if (ghost && NearestEdge(selected!, toScreen, input.Position) is { } edge)
            Place(editor.Parts.EdgeGhost, edge.Screen);
        UiLayout.SetHidden(editor.Parts.EdgeGhost, !ghost);

        DrawHandles(ref editor, selected, toScreen);
    }

    private void DrawHandles(ref XifEditor editor, XifPolygon? selected, Transform2D toScreen)
    {
        List<Vector2> handlePoints = selected is null ? [] : XifEditing.Handles(selected);
        int count = handlePoints.Count;
        if (editor.Handles.Count != count)
        {
            foreach (Entity handle in editor.Handles)
                UiLayout.DeleteSubtree(handle);
            editor.Handles.Clear();
            UiNode layer = UiNode.Wrap(editor.Parts.Lines.IsNull ? editor.Parts.Canvas : editor.Parts.Lines, _visuals).LayerNode;
            for (int i = 0; i < count; i++)
            {
                editor.Handles.Add(layer.Rect(Handle, size: new Vector2(HandleSize, HandleSize))
                    .IgnoreLayout().Tag<UiOverlayItem>().Hidden());
            }
        }

        for (int i = 0; i < count; i++)
        {
            Entity handle = editor.Handles[i];
            Place(handle, toScreen * handlePoints[i]);
            UiLayout.SetHidden(handle, false);
            bool hovered = i == editor.HoverPoint && !editor.Dragging;
            Color color = i == editor.Point ? SelectedHandle : hovered ? HoverHandle : Handle;
            float size = hovered || (editor.Dragging && i == editor.Point) ? HoverHandleSize : HandleSize;
            if (handle.TryGetComponent(out FillColor fill) && fill.Value != color)
            {
                handle.GetComponent<FillColor>().Value = color;
                handle.AddTag<Dirty>();
            }
            if (handle.TryGetComponent(out Rectangle rect) && rect.Bounds.Size.X != size)
            {
                handle.GetComponent<Rectangle>() = new Rectangle(new Rect2(new Vector2(-size / 2, -size / 2), new Vector2(size, size)));
                handle.AddTag<Dirty>();
            }
        }
    }

    private static void HideShapes(ref XifEditor editor)
    {
        foreach (Entity fill in editor.Parts.Fills ?? [])
            UiLayout.SetHidden(fill, true);
        UiLayout.SetHidden(editor.Parts.Shape, true);
        UiLayout.SetHidden(editor.Parts.Origin, true);
        UiLayout.SetHidden(editor.Parts.EdgeGhost, true);
        foreach (Entity handle in editor.Handles)
            UiLayout.SetHidden(handle, true);
    }

    /// <summary>
    /// Points a floating shape at screen points. The shape sits at the
    /// origin of its layer, so its points are the screen points themselves.
    /// </summary>
    private static void SetShape(Entity entity, List<Vector2> points, bool closed)
    {
        if (entity.IsNull || !entity.HasComponent<UiShape>())
            return;

        if (points.Count < 2)
        {
            UiLayout.SetHidden(entity, true);
            return;
        }

        Place(entity, Vector2.Zero);
        UiLayout.SetHidden(entity, false);
        ref UiShape shape = ref entity.GetComponent<UiShape>();
        shape.Points = [.. points];
        shape.Closed = closed;
        entity.AddTag<Dirty>();
    }

    // ------------------------------------------------------------- readouts

    private static void SyncLabels(ref XifEditor editor, Xif item)
    {
        XifPolygon? selected = Selected(editor, item);
        UiText.Set(editor.Parts.Title, $"EDIT XIF · {editor.Host.Title.ToUpperInvariant()}");
        List<Vector2> handles = selected is null ? [] : XifEditing.Handles(selected);
        UiText.Set(editor.Parts.PointLabel, editor.Point < handles.Count
            ? $"{editor.Point + 1}/{handles.Count}  {handles[editor.Point].X:0.#}, {handles[editor.Point].Y:0.#}"
            : "");

        XifShaderRef current = item.ShaderRef;
        int index = ShaderIndex(editor.Host.Shaders, current);
        string shader = index >= 0 ? editor.Host.Shaders[index].Label : current.IsNone ? "NONE" : "NOT ALLOWED HERE";
        UiText.Set(editor.Parts.Shader, shader);
        UiText.Set(editor.Parts.Status, editor.Status ?? "");
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

    /// <summary>Sets a background to its colour with the host's opacity on its alpha.</summary>
    private static void Fade(Entity entity, Color colour, float opacity)
    {
        if (entity.IsNull || !entity.HasComponent<FillColor>())
            return;
        var faded = new Color(colour, colour.A * opacity);
        ref FillColor fill = ref entity.GetComponent<FillColor>();
        if (fill.Value == faded)
            return;
        fill.Value = faded;
        entity.AddTag<Dirty>();
    }

    private static void Place(Entity entity, Vector2 position)
    {
        if (entity.IsNull || !entity.HasComponent<Position>())
            return;

        if (UiLayout.Move(entity, position))
            entity.AddTag<Dirty>();
    }

    private static void SetField(Entity entity, string value)
    {
        ref TextField field = ref entity.GetComponent<TextField>();
        if (field.Value != value)
            field.Value = value;
        UiText.Set(entity, value);
    }
}
