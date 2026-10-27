using Friflo.Engine.ECS;
using Godot;

namespace Writer.Tools.Animator;

/// <summary>UI entities the animator systems read or write.</summary>
public struct AnimatorUi : IComponent
{
    public Entity Status;
    public Entity Preview;
    public Entity Timeline;
    public Entity BoneList;
    public Entity AttachmentList;

    /// <summary>Header slot that holds the clip dropdown button.</summary>
    public Entity ClipList;

    /// <summary>
    /// Top layer for floating UI: menus, dropdown lists and the history panel.
    /// It draws above the overlay canvases, so lists opened from the header
    /// are not hidden behind the preview.
    /// </summary>
    public Entity MenuLayer;

    /// <summary>The key menu and its two options; the menu system dims the one that does not apply.</summary>
    public Entity KeyMenu;
    public Entity KeyMenuAdd;
    public Entity KeyMenuDelete;

    /// <summary>Screen size the layout was built for, so floating menus can stay on screen.</summary>
    public Vector2 ScreenSize;

    /// <summary>Button of the clip dropdown; rebuilt with the clip list.</summary>
    public Entity ClipDropdown;
    public Entity ClipName;
    public Entity LoopButton;
    public Entity PathField;
    public Entity DirtyLabel;

    /// <summary>Floating bone context menu and the field used to rename a bone.</summary>
    public Entity BoneMenu;
    public Entity BoneNameField;

    /// <summary>The ADD SVG picker and the list its choices are built into.</summary>
    public Entity SvgMenu;
    public Entity SvgMenuList;

    /// <summary>Floating transform inspector over the preview, and its fields.</summary>
    public Entity Inspector;
    public Entity InspectorTitle;
    public Entity InspectPosX;
    public Entity InspectPosY;
    public Entity InspectRotation;
    public Entity InspectScaleX;
    public Entity InspectScaleY;
    public Entity InspectLayer;

    /// <summary>Floating hover explanation, moved by the tooltip system.</summary>
    public Entity Tooltip;
}
