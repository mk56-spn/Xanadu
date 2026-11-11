using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Rendering;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>The layers panel's size and where it docks.</summary>
public static class EditorLayersLayout
{
    public const float Width = 300f;
    public const float Pad = 8f;
    public const float RowHeight = 26f;
    public const float ToggleWidth = 26f;
    public const float SelectWidth = 40f;

    /// <summary>Rows shown at once; the ▲ ▼ buttons scroll through more.</summary>
    public const int MaxRows = 10;

    private const float Gap = 6f;

    /// <summary>
    /// The panel's top-right corner: at the view's right edge, under the bar,
    /// or under the group panel while that is open.
    /// </summary>
    public static Vector2 TopRight(Vector2 screen, bool groupOpen)
    {
        float top = groupOpen
            ? EditorGroupLayout.PanelArea(screen).End.Y
            : EditorGroupLayout.BarArea(screen).End.Y;
        return new Vector2(EditorGroupLayout.PanelArea(screen).End.X - Gap, top + Gap);
    }
}

/// <summary>
/// Runs the layers panel (Docs/render-architecture.md step 4): the view bar's
/// LAYERS button opens it, and its rows list the layers front first with the
/// player wedge fixed among them. A row's label makes it current; H, L and S
/// hide, lock and solo it in the editor; SEL selects what's on it. + FRONT and
/// + BACK add an empty layer beside the current one, and NAME renames it.
/// </summary>
public sealed class EditorLayersPanelSystem(EditorHistory history)
    : QuerySystem<EditorLayers, EditorLayersPanel, NoteSelection, PlayfieldView>
{
    private readonly ButtonPress _presses = new();

    /// <summary>The level before the name field was focused; leaving the field makes the rename one undo step.</summary>
    private EditorSnapshot? _beforeName;

    protected override void OnUpdate()
    {
        foreach (Entity editor in Query.ToEntityList())
            Update(editor);
    }

    private void Update(Entity editor)
    {
        if (!editor.HasComponent<LevelLayers>())
            return;

        ref EditorLayers layers = ref editor.GetComponent<EditorLayers>();
        ref LevelLayers table = ref editor.GetComponent<LevelLayers>();
        EditorLayersPanel panel = editor.GetComponent<EditorLayersPanel>();
        CommandBuffer buffer = CommandBuffer;

        if (_presses.Started(panel.Toggle))
            layers.Open = !layers.Open;
        Tint(panel.Toggle, layers.Open, buffer);
        UiLayout.SetHidden(panel.Panel, buffer, !layers.Open);
        if (!layers.Open)
            return;

        Place(panel.Panel, editor, buffer);
        List<EditorLayerRow> rows = EditorLayerEdits.Rows(editor.Store, layers, table);
        int current = layers.Current;
        if (!rows.Exists(row => row.Layer == current))
            layers.Current = ItemLayer.Gameplay;

        HandlePresses(editor, ref layers, ref table, panel, rows);
        rows = EditorLayerEdits.Rows(editor.Store, layers, table);
        layers.Scroll = System.Math.Clamp(layers.Scroll, 0, System.Math.Max(0, rows.Count - EditorLayersLayout.MaxRows));

        for (int i = 0; i < panel.Rows.Length; i++)
        {
            int at = layers.Scroll + i;
            bool used = at < rows.Count;
            UiLayout.SetHidden(panel.Rows[i].Row, buffer, !used);
            if (used)
                Present(panel.Rows[i], rows[at], layers, table, buffer);
        }

        // The arrows only show when there are more layers than rows.
        bool scrolls = rows.Count > EditorLayersLayout.MaxRows;
        UiLayout.SetHidden(panel.Up, buffer, !scrolls);
        UiLayout.SetHidden(panel.Down, buffer, !scrolls);
        UiLayout.SetDisabled(panel.Up, layers.Scroll == 0);
        UiLayout.SetDisabled(panel.Down, layers.Scroll + EditorLayersLayout.MaxRows >= rows.Count);
        ShowName(panel.Name, layers, ref table);

        // MOVE stops at the ends; DELETE only takes an empty layer.
        int currentLayer = layers.Current;
        int place = rows.FindIndex(row => row.Layer == currentLayer);
        bool wedge = currentLayer == 0;
        Usable(panel.MoveForward, !wedge && place > 0, buffer);
        Usable(panel.MoveBack, !wedge && place >= 0 && place < rows.Count - 1, buffer);
        Usable(panel.Delete, !wedge && EditorLayerEdits.CountOn(editor.Store, currentLayer) == 0, buffer);
        ShowPass(editor, layers, table, panel, buffer);
    }

    private void HandlePresses(
        Entity editor,
        ref EditorLayers layers,
        ref LevelLayers table,
        EditorLayersPanel panel,
        List<EditorLayerRow> rows)
    {
        for (int i = 0; i < panel.Rows.Length; i++)
        {
            int at = layers.Scroll + i;
            EditorLayerRowUi ui = panel.Rows[i];
            bool used = at < rows.Count;
            int layer = used ? rows[at].Layer : 0;

            // Every button is read each frame so its press latch re-arms.
            bool label = _presses.Started(ui.Label, used && ui.Label.Tags.Has<Pressed>());
            bool hide = _presses.Started(ui.Hide, used && ui.Hide.Tags.Has<Pressed>());
            bool lockPress = _presses.Started(ui.Lock, used && ui.Lock.Tags.Has<Pressed>());
            bool solo = _presses.Started(ui.Solo, used && ui.Solo.Tags.Has<Pressed>());
            bool select = _presses.Started(ui.Select, used && ui.Select.Tags.Has<Pressed>());
            if (!used)
                continue;

            if (label)
                layers.Current = layer;
            if (layer == 0)
                continue;

            if (hide)
                Flip(layers.Hidden ??= new HashSet<int>(), layer);
            if (lockPress)
                Flip(layers.Locked ??= new HashSet<int>(), layer);
            if (solo)
                layers.Solo = layers.Solo == layer ? 0 : layer;
            if (hide || lockPress || solo)
                DropUnpickable(editor, layers);
            if (select && layers.Picks(layer))
                editor.GetComponent<NoteSelection>().SetMany(EditorLayerEdits.OnLayer(editor.Store, layer));
        }

        bool moveForward = _presses.Started(panel.MoveForward);
        bool moveBack = _presses.Started(panel.MoveBack);
        bool delete = _presses.Started(panel.Delete);
        if (moveForward || moveBack || delete)
        {
            // Items, names and passes move together: one undo step.
            EditorSnapshot before = history.Capture();
            bool changed = delete
                ? EditorLayerEdits.DeleteLayer(editor.Store, ref layers, ref table, layers.Current)
                : EditorLayerEdits.MoveLayer(editor.Store, ref layers, ref table, layers.Current, moveForward);
            if (changed)
                history.Record(before, history.Capture());
        }

        bool front = _presses.Started(panel.Front);
        bool back = _presses.Started(panel.Back);
        if (front || back)
        {
            // Making room can move items and table entries, which is one undo step.
            EditorSnapshot before = history.Capture();
            EditorLayerEdits.Insert(editor.Store, ref layers, ref table, layers.Current, front);
            history.Record(before, history.Capture());
        }

        if (_presses.Started(panel.Up))
            layers.Scroll--;
        if (_presses.Started(panel.Down))
            layers.Scroll++;

        if (layers.Current != 0)
            HandlePass(editor, ref layers, ref table, panel);
    }

    /// <summary>The pass row: the current layer's pass kind, its shader, and keyed instance values.</summary>
    private void HandlePass(Entity editor, ref EditorLayers layers, ref LevelLayers table, EditorLayersPanel panel)
    {
        // Read every button each frame so its press latch re-arms.
        bool kind = _presses.Started(panel.Pass);
        int? shader = null;
        if (!panel.PassShader.IsNull && panel.PassShader.TryGetComponent(out DropdownChanged chosen))
        {
            shader = System.Math.Clamp(chosen.Value, 0, ShaderLimits.Count);
            CommandBuffer.RemoveComponent<DropdownChanged>(panel.PassShader.Id);
        }

        bool param = _presses.Started(panel.PassParam);
        bool channel = _presses.Started(panel.PassChannel);
        bool down = _presses.Started(panel.PassDown);
        bool up = _presses.Started(panel.PassUp);
        bool clear = _presses.Started(panel.PassClear);
        if (!kind && shader is null && !param && !channel && !down && !up && !clear)
            return;

        int layer = layers.Current;
        if (kind)
        {
            EditorSnapshot before = history.Capture();
            LevelLayer changed = table.Edit(layer);
            changed.Pass = NextPass(changed.Pass);
            table.Prune(layer);
            history.Record(before, history.Capture());
            layers.PassParam = 0;
            layers.PassChannel = 0;
        }

        int current = table.Layers.TryGetValue(layer, out LevelLayer? existing) && existing is not null
            ? existing.PassShader
            : 0;
        if (shader is { } slot && slot != current)
        {
            // Keys of settings the new shader also declares carry over.
            EditorSnapshot before = history.Capture();
            LevelLayer changed = table.Edit(layer);
            changed.PassShader = slot;
            EditorShaderKeyEdits.KeepFor(changed.PassKeys, PassParams(editor, slot));
            table.Prune(layer);
            history.Record(before, history.Capture());
            layers.PassParam = 0;
            layers.PassChannel = 0;
        }

        if (!table.Layers.TryGetValue(layer, out LevelLayer? found) || found is null)
            return;
        LevelLayer entry = found;
        List<InstanceUniform> uniforms = PassParams(editor, entry.PassShader);
        if (uniforms.Count == 0)
            return;

        if (layers.PassParam < 0 || layers.PassParam >= uniforms.Count)
            layers.PassParam = 0;
        if (param)
        {
            layers.PassParam = (layers.PassParam + 1) % uniforms.Count;
            layers.PassChannel = 0;
        }

        InstanceUniform uniform = uniforms[System.Math.Clamp(layers.PassParam, 0, uniforms.Count - 1)];
        if (layers.PassChannel < 0 || layers.PassChannel >= uniform.Channels)
            layers.PassChannel = 0;
        if (channel)
            layers.PassChannel = (layers.PassChannel + 1) % uniform.Channels;

        float now = editor.TryGetComponent(out Playhead playhead) ? playhead.TimeMs / 1000f : 0f;
        if (down || up || clear)
        {
            EditorSnapshot before = history.Capture();
            int edited = layers.PassChannel;
            if (down)
                EditorShaderKeyEdits.KeyValue(entry.PassKeys, uniform, edited, -0.05f, now);
            if (up)
                EditorShaderKeyEdits.KeyValue(entry.PassKeys, uniform, edited, 0.05f, now);
            if (clear)
                EditorShaderKeyEdits.Clear(entry.PassKeys, uniform);
            history.Record(before, history.Capture());
        }
    }

    private static LayerPassKind NextPass(LayerPassKind pass) => pass switch
    {
        LayerPassKind.None => LayerPassKind.BackgroundReader,
        LayerPassKind.BackgroundReader => LayerPassKind.SelfContained,
        _ => LayerPassKind.None,
    };

    /// <summary>The instance uniforms the current pass shader declares.</summary>
    private static List<InstanceUniform> PassParams(Entity editor, int slot)
    {
        if (slot == 0 || !editor.TryGetComponent(out LevelShaders shaders))
            return new List<InstanceUniform>();

        return shaders.Get(slot) is { } entry
            ? ShaderSourceUniforms.InstanceUniforms(entry.Source)
            : new List<InstanceUniform>();
    }

    /// <summary>What the pass row says, and which of its buttons apply, for the current layer.</summary>
    private static void ShowPass(
        Entity editor,
        in EditorLayers layers,
        in LevelLayers table,
        EditorLayersPanel panel,
        CommandBuffer buffer)
    {
        bool wedge = layers.Current == 0;
        LevelLayer? entry = !wedge &&
            table.Layers is not null &&
            table.Layers.TryGetValue(layers.Current, out LevelLayer? found)
                ? found
                : null;
        LayerPassKind pass = entry?.Pass ?? LayerPassKind.None;
        int slot = entry?.PassShader ?? 0;

        UiText.Set(
            panel.Pass,
            wedge ? "PASS -" : $"PASS {PassName(pass)}",
            buffer);
        Tint(panel.Pass, pass != LayerPassKind.None, buffer);
        EditorGroupSystem.ShowShaderChoice(
            panel.PassShader,
            editor.TryGetComponent(out LevelShaders shaders) ? shaders : null,
            slot,
            buffer);
        Tint(panel.PassShader, slot != 0, buffer);
        UiLayout.SetDisabled(panel.Pass, wedge);
        UiLayout.SetDisabled(panel.PassShader, wedge);

        List<InstanceUniform> uniforms = PassParams(editor, slot);
        if (uniforms.Count == 0)
        {
            UiText.Set(panel.PassParam, "PARAM", buffer);
            UiText.Set(panel.PassChannel, "CH", buffer);
            UiText.Set(panel.PassValue, "", buffer);
            Tint(panel.PassParam, false, buffer);
            Tint(panel.PassChannel, false, buffer);
            Tint(panel.PassDown, false, buffer);
            Tint(panel.PassUp, false, buffer);
            Tint(panel.PassClear, false, buffer);
            return;
        }

        int selected = System.Math.Clamp(layers.PassParam, 0, uniforms.Count - 1);
        InstanceUniform uniform = uniforms[selected];
        int channel = System.Math.Clamp(layers.PassChannel, 0, uniform.Channels - 1);
        float seconds = editor.TryGetComponent(out Playhead playhead) ? playhead.TimeMs / 1000f : 0f;
        Vector4 value = ShaderKeyTrack.ValueAt(entry?.PassKeys ?? new List<ShaderKey>(), uniform.Name, seconds, uniform.Default);
        int keys = entry?.PassKeys.Count(key => key.Param == uniform.Name) ?? 0;

        UiText.Set(panel.PassParam, uniform.Name.ToUpperInvariant(), buffer);
        UiText.Set(panel.PassChannel, $"CH {channel + 1}/{uniform.Channels}", buffer);
        UiText.Set(panel.PassValue, Format(value, uniform.Channels), buffer);
        Tint(panel.PassParam, true, buffer);
        Tint(panel.PassChannel, true, buffer);
        Tint(panel.PassDown, true, buffer);
        Tint(panel.PassUp, true, buffer);
        Tint(panel.PassClear, keys > 0, buffer);
    }

    private static string PassName(LayerPassKind pass) => pass switch
    {
        LayerPassKind.BackgroundReader => "BG",
        LayerPassKind.SelfContained => "SELF",
        _ => "NONE",
    };

    private static string Format(Vector4 value, int channels) => EditorShaderKeyEdits.Format(value, channels);

    /// <summary>Takes items that can no longer be picked out of the selection.</summary>
    private static void DropUnpickable(Entity editor, in EditorLayers layers)
    {
        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        var kept = new List<Entity>();
        foreach (Entity item in selection.AllNotes)
        {
            if (!EditorItems.IsItem(item) || layers.Picks(ItemLayer.Of(item).Layer))
                kept.Add(item);
        }

        if (kept.Count != selection.AllNotes.Count)
            selection.SetMany(kept);
    }

    /// <summary>
    /// The name field renames the current layer as it's typed in, and shows its
    /// name otherwise. The rename is one undo step, recorded when the field is left.
    /// </summary>
    private void ShowName(Entity field, in EditorLayers layers, ref LevelLayers table)
    {
        if (field.IsNull)
            return;

        UiLayout.SetDisabled(field, layers.Current == 0);
        ref TextField text = ref field.GetComponent<TextField>();
        bool editing = field.Tags.Has<TextFieldFocused>() && layers.Current != 0;
        if (editing)
        {
            _beforeName ??= history.Capture();
            if (table.NameOf(layers.Current) != text.Value.Trim())
                table.SetName(layers.Current, text.Value);
            return;
        }

        if (_beforeName is { } before)
        {
            history.Record(before, history.Capture());
            _beforeName = null;
        }

        string shown = layers.Current == 0 ? "PLAYER" : table.NameOf(layers.Current);
        if (text.Value != shown)
            text.Value = shown;
    }

    private static void Present(EditorLayerRowUi ui, EditorLayerRow row, in EditorLayers layers, in LevelLayers table, CommandBuffer buffer)
    {
        LayerPassKind rowPass = table.Layers is not null && table.Layers.TryGetValue(row.Layer, out LevelLayer? rowEntry) && rowEntry is not null
            ? rowEntry.Pass
            : LayerPassKind.None;
        UiText.Set(ui.Label, Label(row, table.NameOf(row.Layer), rowPass), buffer);
        Color fill = row.Layer == layers.Current ? Palette.Selected : UiTheme.PanelAlt;
        Paint(ui.Label, fill, buffer);

        bool wedge = row.IsWedge;
        Tint(ui.Hide, layers.Hidden?.Contains(row.Layer) == true, buffer);
        Tint(ui.Lock, layers.Locked?.Contains(row.Layer) == true, buffer);
        Tint(ui.Solo, layers.Solo == row.Layer && !wedge, buffer);
        Paint(ui.Select, Palette.PrimaryMuted, buffer);
        // The wedge can't be hidden, locked, soloed or selected, so its row has only its label.
        UiLayout.SetHidden(ui.Hide, buffer, wedge);
        UiLayout.SetHidden(ui.Lock, buffer, wedge);
        UiLayout.SetHidden(ui.Solo, buffer, wedge);
        UiLayout.SetHidden(ui.Select, buffer, wedge);
        UiLayout.SetDisabled(ui.Select, row.Count == 0 || !layers.Picks(row.Layer));
    }

    /// <summary>A row's label: the layer's number, its name, and how many items are on it.</summary>
    /// <summary>A row's label: its number, name and item count, and BG or SELF when the layer has a pass.</summary>
    public static string Label(EditorLayerRow row, string name, LayerPassKind pass = LayerPassKind.None)
    {
        if (row.IsWedge)
            return "      PLAYER (fixed)";
        string number = row.Layer > 0 ? $"+{row.Layer}" : row.Layer.ToString();
        string label = name.Length > 0 ? $"{number,4}  {name} · {row.Count}" : $"{number,4}  · {row.Count}";
        return pass == LayerPassKind.None ? label : $"{label}  [{PassName(pass)}]";
    }

    private static void Place(Entity panel, Entity editor, CommandBuffer buffer)
    {
        if (!panel.HasComponent<Position>() || !panel.TryGetComponent(out Rectangle rect))
            return;

        Vector2 size = rect.Bounds.Size;
        Vector2 corner = EditorLayersLayout.TopRight(
            editor.GetComponent<PlayfieldView>().ScreenSize,
            EditorGroupState.IsOpen(editor));
        var centre = new Vector2(corner.X - size.X / 2f, corner.Y + size.Y / 2f);
        ref Position position = ref panel.GetComponent<Position>();
        if (position.Value == centre)
            return;

        position.Value = centre;
        buffer.AddTag<Moved>(panel.Id);
    }

    private static void Flip(HashSet<int> set, int layer)
    {
        if (!set.Remove(layer))
            set.Add(layer);
    }

    /// <summary>Bright blue only while on (the bright-blue-means-on rule).</summary>
    /// <summary>A plain action button: muted blue while it can act, slate and disabled while it can't.</summary>
    private static void Usable(Entity button, bool usable, CommandBuffer buffer)
    {
        UiLayout.SetDisabled(button, buffer, !usable);
        Paint(button, usable ? Palette.PrimaryMuted : SceneColors.Muted, buffer);
    }

    private static void Tint(Entity button, bool on, CommandBuffer buffer) =>
        Paint(button, on ? Palette.Primary : Palette.PrimaryMuted, buffer);

    private static void Paint(Entity button, Color color, CommandBuffer buffer)
    {
        if (button.IsNull || !button.HasComponent<ButtonVisual>())
            return;

        ref ButtonVisual visual = ref button.GetComponent<ButtonVisual>();
        if (visual.Normal == color)
            return;

        visual.Normal = color;
        visual.Hover = color.Lerp(Colors.White, 0.16f);
        buffer.AddTag<Dirty>(button.Id);
    }
}
