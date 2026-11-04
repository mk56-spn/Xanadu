namespace Xanadu.Ecs;

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
    public const int AnimatorTooltip = 7;

    // Lab: a tool of its own. The engine settings panel covers the experiment's panel.
    public const int LabSettings = 8;

    // Menu scenes. The shared background and the settings backdrop never coexist.
    public const int MenuBackground = 99;
    public const int Menu = 100;

    public const int EditorWarning = 850;

    public const int PlayHud = 900;

    // Editor, in the order its panels stack.
    public const int EditorSidebar = 941;
    public const int EditorChrome = 960;
    public const int EditorStartPoints = 961;
    public const int EditorMetadata = 962;
    public const int EditorRail = 964;
    public const int EditorLists = 970;
    public const int EditorHelp = 970;
    public const int EditorGroupsScrim = 975;
    public const int EditorGroupsBack = 976;
    public const int EditorGroupsTracks = 977;
    public const int EditorGroupsTicks = 978;
    public const int EditorGroupsBars = 979;
    public const int EditorGroups = 980;
    public const int EditorGroupsPreview = 981;

    // Scene system overlays. These share values with the editor layers above.
    public const int Confirmation = 970;
    public const int Pause = 980;
    public const int SongMenu = 980;

    public const int Toast = 990;
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
