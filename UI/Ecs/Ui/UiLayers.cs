namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// Every UI layer's draw order, bottom to top, and the one place to change it.
/// Name a layer here rather than passing a number to <c>UiNode.Layer</c>. Layers
/// sharing a value draw in no defined order against each other, so give a new
/// layer a value of its own.
/// </summary>
public static class UiLayers
{
    /// <summary>The default layer, and the base of the tools that have one.</summary>
    public const int Base = 0;

    // Animator: a tool of its own, never on screen with the game.
    public const int AnimatorMenu = 6;

    /// <summary>
    /// The XIF editor, opened over everything else in the animator. Well clear
    /// of the menu layer: overlays inside a layer add their own z on top of
    /// its number, so a menu's picker cursors would otherwise draw over it.
    /// </summary>
    public const int AnimatorXif = 30;

    /// <summary>
    /// The XIF editor's field lines: over its panel, so the panel can fade
    /// while they stay solid, and clear of the panel's overlays.
    /// </summary>
    public const int AnimatorXifLines = 45;

    /// <summary>
    /// The item the XIF editor holds where it really is (a part on its bone),
    /// drawn by its host: above the editor's see-through backing (and its
    /// overlays), under its field lines.
    /// </summary>
    public const int AnimatorXifItem = 42;

    public const int AnimatorTooltip = 60;

    // Lab: a tool of its own. The engine settings panel covers the experiment's panel.
    public const int LabSettings = 70;

    // The shader editor's values column, over its code and chrome (the shader tool and the lab's editor tabs).
    public const int ShaderEditorValues = 72;

    // Menu scenes. The shared background and the settings backdrop never coexist.
    public const int MenuBackground = 99;
    public const int Menu = 100;

    public const int EditorWarning = 850;

    public const int PlayHud = 900;

    // Editor, in the order its panels stack.
    public const int EditorSidebar = 941;
    public const int EditorChrome = 960;
    public const int EditorMetadata = 962;

    /// <summary>The XIF editor for the selected polygon, beside the tool strip.</summary>
    public const int EditorXif = 963;

    public const int EditorRail = 965;
    public const int EditorLists = 970;
    public const int EditorHelp = 970;
    public const int EditorGroupsScrim = 975;
    public const int EditorGroupsBack = 976;
    public const int EditorGroups = 980;

    /// <summary>The group panel's drawn timeline: beat lines, labels, effect bands, pins, the playhead and the ease curve, over the panel's controls.</summary>
    public const int EditorGroupsCanvas = 981;

    /// <summary>The bar at the top centre of the view and its drop-downs: above the open panel and its canvas.</summary>
    public const int EditorGroupBar = 982;

    /// <summary>The layers panel the bar's LAYERS button opens, docked to the view's right edge.</summary>
    public const int EditorLayers = 983;

    /// <summary>
    /// The tool groups' flyouts, beside the tool strip: over the tool settings,
    /// the XIF editor and the view's group bar, which a long flyout runs under.
    /// It shares a number with the layers panel, which docks to the far side.
    /// </summary>
    public const int EditorToolFlyout = 983;

    /// <summary>
    /// The SHD lists of the group and layers panels: above both, so an open
    /// list takes presses before the panel controls it covers.
    /// </summary>
    public const int EditorShaderChoice = 984;

    /// <summary>The shared ease picker the group panel's EASE buttons open: above the panel and its lists.</summary>
    public const int EditorEasePicker = 985;

    /// <summary>The editor's modal dialogs: the song picker, recovery, shaders and map colours.</summary>
    public const int EditorDialogs = 986;

    // Scene system overlays. These share values with the editor layers above.
    public const int Confirmation = 970;
    public const int Pause = 980;
    public const int SongMenu = 980;

    public const int Toast = 990;

    /// <summary>The built-in docs, dropped down over any screen and its toasts.</summary>
    public const int Docs = 992;

    public const int Focus = 995;
    public const int Tooltip = 996;

    /// <summary>The fade between scenes covers everything but the debug views.</summary>
    public const int SceneTransition = 1000;

    /// <summary>The visual test harness, above any scene panel that reaches under it.</summary>
    public const int VisualTestSidebar = 1001;

    // Debug views. The pressed overlay and the UI tree overlay share a value.
    public const int DebugPressed = 1002;
    public const int DebugUiTree = 1002;
    public const int ShaderTuner = 1003;
    public const int FpsCounter = 1004;
}
