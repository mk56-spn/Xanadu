using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// The level editor's own view of the layers (docs/render-architecture.md
/// step 4): which are hidden, locked or soloed, empty layers added from the
/// layers panel, and the row picked in the panel. None of it is saved; names
/// and passes are level data, in <see cref="LevelLayers"/>. Layer 0 stands for
/// the player wedge.
/// </summary>
public struct EditorLayers : IComponent
{
    /// <summary>Whether the layers panel is open.</summary>
    public bool Open;

    /// <summary>The row picked in the panel: new layers are added beside it, and the name field edits it.</summary>
    public int Current;

    /// <summary>The one layer drawn and picked on its own, or 0 for none.</summary>
    public int Solo;

    /// <summary>The first row the panel shows, when there are more layers than rows.</summary>
    public int Scroll;

    /// <summary>The pass shader's instance uniform, and component, the panel's pass row edits.</summary>
    public int PassParam;
    public int PassChannel;

    public HashSet<int> Hidden;
    public HashSet<int> Locked;

    /// <summary>Layers added from the panel; they stay listed while empty.</summary>
    public HashSet<int> Kept;

    public static EditorLayers Create() => new()
    {
        Current = ItemLayer.Gameplay,
        Hidden = new HashSet<int>(),
        Locked = new HashSet<int>(),
        Kept = new HashSet<int>(),
    };

    /// <summary>Whether a layer draws in the editor: not hidden, and the soloed one if there is one. The wedge always draws.</summary>
    public readonly bool Shows(int layer) =>
        layer == 0 || (Hidden?.Contains(layer) != true && (Solo == 0 || Solo == layer));

    /// <summary>Whether items on a layer can be picked or selected on the canvas.</summary>
    public readonly bool Picks(int layer) => Shows(layer) && Locked?.Contains(layer) != true;

    /// <summary>The editor's layer state, or one with everything shown when it has none.</summary>
    public static EditorLayers Of(Entity editor) =>
        !editor.IsNull && editor.TryGetComponent(out EditorLayers layers) ? layers : default;

    /// <summary>Whether an item can be picked on the canvas in this editor.</summary>
    public static bool CanPick(Entity editor, Entity item) =>
        Of(editor).Picks(ItemLayer.Of(item).Layer);
}

/// <summary>One row of the layers panel and its buttons.</summary>
public readonly record struct EditorLayerRowUi(
    Entity Row,
    Entity Label,
    Entity Hide,
    Entity Lock,
    Entity Solo,
    Entity Select);

/// <summary>The layers panel's pieces, on the editor entity.</summary>
public struct EditorLayersPanel(
    Entity toggle,
    Entity panel,
    EditorLayerRowUi[] rows,
    Entity front,
    Entity back,
    Entity up,
    Entity down,
    Entity name,
    Entity pass,
    Entity passShader,
    Entity passParam,
    Entity passChannel,
    Entity passDown,
    Entity passValue,
    Entity passUp,
    Entity passClear) : IComponent
{
    /// <summary>The view bar's LAYERS button, which opens and closes the panel.</summary>
    public Entity Toggle = toggle;
    public Entity Panel = panel;
    public EditorLayerRowUi[] Rows = rows;
    public Entity Front = front;
    public Entity Back = back;
    public Entity Up = up;
    public Entity Down = down;

    /// <summary>The current layer's name.</summary>
    public Entity Name = name;

    /// <summary>The current layer's pass: its kind, shader, and keyed instance uniforms.</summary>
    public Entity Pass = pass;
    public Entity PassShader = passShader;
    public Entity PassParam = passParam;
    public Entity PassChannel = passChannel;
    public Entity PassDown = passDown;
    public Entity PassValue = passValue;
    public Entity PassUp = passUp;
    public Entity PassClear = passClear;
}

/// <summary>The sidebar's layer and z steppers and shader-source choice for the selection, on the editor entity.</summary>
public struct EditorLayerInspector(Entity section, Entity layer, Entity z, Entity shaderSource) : IComponent
{
    public Entity Section = section;
    public Entity Layer = layer;
    public Entity Z = z;
    public Entity ShaderSource = shaderSource;
}
