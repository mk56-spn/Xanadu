using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Tools.Animator;

/// <summary>UI entities the animator systems read or write.</summary>
public struct AnimatorUi : IComponent
{
    public Entity Status;
    public Entity Preview;
    public Entity Timeline;
    public Entity BoneList;
    public Entity AttachmentList;
    public Entity ChainList;

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

    /// <summary>The CONFIG dropdown button in the header and its floating length panel.</summary>
    public Entity ConfigButton;
    public Entity ClipLengthPanel;
    public Entity ClipLengthField;

    /// <summary>The NEW CLIP dialog, its dimmer and the fields that describe the clip.</summary>
    public Entity NewClipBackdrop;
    public Entity NewClipDialog;
    public Entity NewClipDuplicate;
    public Entity NewClipName;
    public Entity NewClipLength;
    public Entity NewClipError;

    /// <summary>The LOOP button above the timeline, next to the game preview buttons.</summary>
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

    /// <summary>
    /// Floating chain inspector under the transform inspector, shown while a
    /// chain is selected, and its fields.
    /// </summary>
    public Entity ChainInspector;
    public Entity ChainTitle;
    public Entity ChainArtButton;
    public Entity ChainBoneButton;
    public Entity ChainOffsetX;
    public Entity ChainOffsetY;
    public Entity ChainColliderButton;
    public Entity ChainColliderX;
    public Entity ChainColliderY;
    public Entity ChainSegments;
    public Entity ChainLength;
    public Entity ChainWidth;
    public Entity ChainGravity;
    public Entity ChainLean;
    public Entity ChainDamping;
    public Entity ChainCollide;

    /// <summary>Floating hover explanation, moved by the tooltip system.</summary>
    public Entity Tooltip;
}
