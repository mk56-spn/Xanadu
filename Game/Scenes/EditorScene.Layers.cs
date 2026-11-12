using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Editor;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Rendering;

namespace Xanadu.Scenes;

/// <summary>
/// The editor's layer controls (Docs/render-architecture.md step 4): the
/// layers panel the view bar's LAYERS button opens, and the selection's layer
/// and z steppers in the sidebar.
/// </summary>
public static class EditorSceneLayers
{
    private const float RowGap = 3f;

    /// <summary>
    /// The layers panel, hidden until opened: a title, a fixed set of row
    /// slots (front first, the player wedge among them), the add and scroll
    /// buttons, and the current layer's name. <c>EditorLayersPanelSystem</c>
    /// fills the rows and places the panel.
    /// </summary>
    internal static void BuildPanel(EntityStore entities, Entity root, Entity editor, Entity toggle)
    {
        float inner = EditorLayersLayout.Width - EditorLayersLayout.Pad * 2;
        float height = EditorLayersLayout.RowHeight;
        UiNode layer = UiNode.Layer(entities, root, UiLayers.EditorLayers, null, UiStage.Full);
        UiNode panel = layer.Column()
            .Width(EditorLayersLayout.Width)
            .AutoHeight()
            .Gap(RowGap)
            .Pad(EditorLayersLayout.Pad)
            .Fill(SceneUi.PanelColor)
            .Input()
            .Main(StackMainAlignment.Start)
            .Hidden();

        EditorSceneUi.SidebarLabel(panel, "LAYERS", inner, 12);

        float label = inner - (EditorLayersLayout.ToggleWidth * 3 + EditorLayersLayout.SelectWidth + RowGap * 4);
        var rows = new EditorLayerRowUi[EditorLayersLayout.MaxRows];
        for (int i = 0; i < rows.Length; i++)
        {
            UiNode row = panel.Row().Size(inner, height).Gap(RowGap).Main(StackMainAlignment.Start);
            Entity name = row.Button("", UiType.Small, UiTheme.PanelAlt)
                .Size(label, height)
                .Align(TextAlign.Left)
                .Tooltip("Make this the current layer: new layers are added beside it, and NAME renames it")
                .Entity;
            Entity hide = Toggle(row, "H", EditorLayersLayout.ToggleWidth, "Hide this layer in the editor; play still draws it");
            Entity lockButton = Toggle(row, "L", EditorLayersLayout.ToggleWidth, "Lock this layer: its items can't be picked or selected on the field");
            Entity solo = Toggle(row, "S", EditorLayersLayout.ToggleWidth, "Solo: draw and pick only this layer in the editor");
            Entity select = Toggle(row, "SEL", EditorLayersLayout.SelectWidth, "Select everything on this layer");
            rows[i] = new EditorLayerRowUi(row.Entity, name, hide, lockButton, solo, select);
        }

        float half = (inner - RowGap * 3 - height * 2) / 2;
        UiNode buttons = panel.Row().Size(inner, height).Gap(RowGap).Main(StackMainAlignment.Start);
        Entity front = Toggle(buttons, "+ FRONT", half, "Add an empty layer just in front of the current one");
        Entity back = Toggle(buttons, "+ BACK", half, "Add an empty layer just behind the current one");
        Entity up = Toggle(buttons, "▲", height, "Scroll the list up");
        Entity down = Toggle(buttons, "▼", height, "Scroll the list down");

        float third = (inner - RowGap * 2) / 3;
        UiNode order = panel.Row().Size(inner, height).Gap(RowGap).Main(StackMainAlignment.Start);
        Entity moveForward = Toggle(order, "MOVE ▲", third, "Move the current layer one place forward, with its items, name and pass");
        Entity moveBack = Toggle(order, "MOVE ▼", third, "Move the current layer one place back, with its items, name and pass");
        Entity delete = Toggle(order, "DELETE", third, "Delete the current layer; only an empty layer can go");

        UiNode nameRow = panel.Row().Size(inner, height).Gap(RowGap).Main(StackMainAlignment.Start);
        nameRow.Text("NAME", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left).Size(52, height);
        Entity field = nameRow.Rect(Palette.SurfaceInset, "", new Vector2(inner - 52 - RowGap, height))
            .Input()
            .With(new TextStyle(13, UiTheme.Text))
            .Align(TextAlign.Left)
            .With(new TextField(""))
            .Tooltip("The current layer's name; click anywhere else to finish")
            .Entity;

        const float shaderWidth = 150f;
        UiNode passRow = panel.Row().Size(inner, height).Gap(RowGap).Main(StackMainAlignment.Start);
        Entity pass = Toggle(passRow, "PASS NONE", inner - shaderWidth - RowGap, "Cycle the current layer's pass: none, a background reader, or self-contained");
        // The pass shader is a choice by name, the same list as the group panel's.
        Entity passShader = Toggle(passRow, "SHD OFF ▾", shaderWidth, "The shader the pass draws with; keys of settings the new shader also has are kept");
        UiNode.Layer(entities, root, UiLayers.EditorShaderChoice, null, UiStage.Full).DropdownList(
            passShader,
            ShaderSlotChoice.Labels(null),
            0,
            optionColor: _ => SceneUi.MutedButtonColor);

        // Same order as the group panel's shader row: setting, channel, value − +, clear.
        UiNode passKeys = panel.Row().Size(inner, height).Gap(RowGap).Main(StackMainAlignment.Start);
        Entity passParam = Toggle(passKeys, "PARAM", 64, "Cycle the pass shader's instance uniform to key");
        Entity passChannel = Toggle(passKeys, "CH", 40, "Which component of the uniform the buttons change");
        Entity passValue = passKeys.Text("", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner - (64 + 40 + 24 + 24 + 60 + RowGap * 5), height)
            .Entity;
        Entity passDown = Toggle(passKeys, "−", 24, "Lower the value at the playhead and key it");
        Entity passUp = Toggle(passKeys, "+", 24, "Raise the value at the playhead and key it");
        Entity passClear = Toggle(passKeys, "CLEAR", 60, "Remove every key of the selected pass setting");

        var built = new EditorLayersPanel(
            toggle,
            panel.Entity,
            rows,
            front,
            back,
            up,
            down,
            field,
            pass,
            passShader,
            passParam,
            passChannel,
            passDown,
            passValue,
            passUp,
            passClear)
        {
            MoveForward = moveForward,
            MoveBack = moveBack,
            Delete = delete,
        };
        editor.AddComponent(built);
    }

    /// <summary>
    /// The selection's layer and z as stepper rows in the sidebar, hidden while
    /// nothing is selected.
    /// </summary>
    internal static void BuildInspector(UiNode panel, Entity editor, float inner)
    {
        const float height = UiMetrics.Control.Xs;
        UiNode section = panel.Column()
            .Width(inner)
            .AutoHeight()
            .Gap(UiMetrics.Space.Xs)
            .Main(StackMainAlignment.Start)
            .Hidden();

        EditorSceneUi.Separator(section, inner);
        Entity layer = Stepper(
            section,
            inner,
            height,
            EditorButtonKind.LayerStep,
            "Move the selection one layer back, past the player wedge if need be",
            "Move the selection one layer forward, past the player wedge if need be");
        EditorSceneUi.Separator(section, inner);
        Entity z = Stepper(
            section,
            inner,
            height,
            EditorButtonKind.ZStep,
            "Lower the selection's z: further back inside its layer",
            "Raise the selection's z: further forward inside its layer");
        EditorSceneUi.Separator(section, inner);
        // Blend is a mode, not a number: a list, not a pair of arrows.
        EditorSceneUi.BuildDropdown(
            section,
            inner,
            EditorDropdownKind.Blend,
            BlendModes.All.Select(BlendModes.Name).ToArray(),
            selected: 0,
            height);
        EditorSceneUi.Separator(section, inner);
        Entity source = Stepper(
            section,
            inner,
            height,
            EditorButtonKind.ShaderSourceStep,
            "Take the shader from the previous group the item is in, or its own",
            "Take the shader from the next group the item is in, or its own");
        editor.AddComponent(new EditorLayerInspector(section.Entity, layer, z, source));
    }

    private static Entity Stepper(UiNode section, float inner, float height, EditorButtonKind kind, string less, string more)
    {
        UiNode row = section.Row().Size(inner, height).Gap(UiTheme.ContainerSpacing).Main(StackMainAlignment.Start);
        Entity readout = row.Text("", UiTheme.SmallFontSize, SceneUi.DimTextColor, TextAlign.Left)
            .Size(inner - (height + UiTheme.ContainerSpacing) * 2, height)
            .Entity;
        row.Button("-", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(EditorButton.Of(kind, -1))
            .Tooltip(less);
        row.Button("+", 13, SceneUi.MutedButtonColor)
            .Size(height, height)
            .With(EditorButton.Of(kind, 1))
            .Tooltip(more);
        return readout;
    }

    private static Entity Toggle(UiNode row, string text, float width, string tooltip) =>
        row.Button(text, UiType.Small, Palette.PrimaryMuted)
            .Size(width, EditorLayersLayout.RowHeight)
            .Tooltip(tooltip)
            .Entity;
}
