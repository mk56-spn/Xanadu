using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Tools.Animator;

/// <summary>UI entities the animator systems read or write.</summary>
public struct AnimatorUi : IComponent
{
    /// <summary>The header's unsaved flag, built the same way as the level editor's.</summary>
    public ShellStatusLabels StatusLabels;

    /// <summary>The selected attachment's self modulate.</summary>
    public Entity ModulateField;
    public Entity Preview;

    /// <summary>The inspector's sliders under its rotation and scale fields.</summary>
    public Entity InspectRotationSlider;
    public Entity InspectScaleSlider;

    /// <summary>The bar floating at the top of the preview: grid, snap and align.</summary>
    public Entity ViewBar;

    /// <summary>
    /// The FLOW tab: its title, the list of clips the current one may fade
    /// into, and the readouts of the selected transition.
    /// </summary>
    public Entity FlowTitle;
    public Entity FlowList;
    public Entity FlowRule;
    public Entity FlowFade;
    public Entity FlowGap;
    public Entity Timeline;

    /// <summary>The clip, time, zoom and snap readout beside the timeline's transport.</summary>
    public Entity TimelineCaption;
    public Entity BoneList;
    public Entity AttachmentList;

    /// <summary>The LAYERS tab's rows; the layer panel system fills it.</summary>
    public Entity LayerList;
    public Entity ChainList;
    public Entity EffectList;

    /// <summary>The collapsible sections of the rig list, titled with their counts.</summary>
    public Entity BoneSection;
    public Entity AttachmentSection;
    public Entity ChainSection;
    public Entity EffectSection;
    public Entity ClothSection;
    public Entity ClothList;

    /// <summary>The POSE / EFFECT / FLOW tab strip, switched to follow the selection.</summary>
    public Entity Tabs;

    /// <summary>The EFFECT tab's title: the selected effect and its bone.</summary>
    public Entity EffectCaption;

    /// <summary>
    /// The shell's regions, which preview mode hides, resizes and recolours:
    /// the viewport column takes the room the tool strip and right panel give
    /// up, and the preview bar takes the timeline's place.
    /// </summary>
    public Entity Header;
    public Entity MainRow;
    public Entity ToolStrip;
    public Entity ViewportColumn;
    public Entity StatusLine;
    public Entity RightPanel;
    public Entity PreviewBar;
    public Entity PreviewCaption;

    /// <summary>The header button that enters and leaves preview mode.</summary>
    public Entity PreviewButton;

    /// <summary>The shared XIF editor, shown beside the inspector while an effect is selected.</summary>
    public Entity XifEditor;

    /// <summary>The shared particle editor, in the XIF editor's place while a particle effect is selected.</summary>
    public Entity ParticleEditor;

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

    /// <summary>The menu a right-click on an override's bar opens.</summary>
    public Entity OverrideMenu;

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

    /// <summary>The preview bar's metronome readouts: the tempo, and how often an attack fires.</summary>
    public Entity MetronomeBpm;
    public Entity MetronomeEvery;

    /// <summary>The EASING header button and the shared ease picker it opens.</summary>
    public Entity EasingButton;
    public Entity EasePicker;

    /// <summary>The file info popup, its dimmer, and the readout under the path field.</summary>
    public Entity InfoBackdrop;
    public Entity InfoPanel;
    public Entity InfoText;
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

    /// <summary>What the selection is, and whether its pose is keyed, under the title.</summary>
    public Entity InspectorSubtitle;

    /// <summary>The bone card under the inspector, and its joint toggles, lit while on.</summary>
    public Entity BoneCard;
    public Entity BoneMoveButton;
    public Entity BoneBendButton;
    public Entity BoneLimitButton;
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

    /// <summary>The event inspector over the timeline, shown while an event is selected, and its fields.</summary>
    public Entity EventInspector;
    public Entity EventTitle;
    public Entity EventName;
    public Entity EventTime;

    /// <summary>The cloth inspector, which takes the transform inspector's spot while a cloth is selected.</summary>
    public Entity ClothInspector;
    public Entity ClothTitle;
    public Entity ClothBoneButton;
    public Entity ClothPinsButton;
    public Entity ClothCollidersButton;
    public Entity ClothFill;

    /// <summary>The cloth inspector's number fields, in <see cref="Commands.AnimatorClothCommands.Setting"/> order.</summary>
    public Entity[] ClothFields;
    public Entity ChainTitle;
    public Entity ChainArtButton;
    public Entity ChainBoneButton;

    /// <summary>Shows and toggles whether the selected chain draws as one sheet or as pieces.</summary>
    public Entity ChainSheetButton;
    public Entity ChainOffsetX;
    public Entity ChainOffsetY;
    public Entity ChainColliderButton;
    public Entity ChainColliderX;
    public Entity ChainColliderY;
    public Entity ChainSegments;
    public Entity ChainLength;
    public Entity ChainWidth;

    /// <summary>The selected chain's own draw layer; empty while it simply draws above the body.</summary>
    public Entity ChainLayer;
    public Entity ChainGravity;
    public Entity ChainLean;
    public Entity ChainDamping;
    public Entity ChainCollide;

    /// <summary>Floating hover explanation, moved by the tooltip system.</summary>
    public Entity Tooltip;
}
