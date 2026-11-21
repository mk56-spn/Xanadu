using Xanadu.Ui.Systems.Input;

namespace Xanadu.Model.Settings;

/// <summary>
/// The game's bands of the function keys, after the engine's (<see cref="DebugKeys"/>):
/// gameplay views F6–F8, editor views F9–F11. A new view takes its band's spare
/// slot; it never fills a hole in another band.
/// </summary>
public static class GameDebugKeys
{
    /// <summary>F6: a graph of the play body's x and y speed over song time.</summary>
    public const Key SpeedGraph = Key.F6;

    /// <summary>F7: gameplay HUD layout editing.</summary>
    public const Key HudLayout = Key.F7;

    /// <summary>F8: ground movement in play: skid (friction fields stop you) or hold the direction key.</summary>
    public const Key GroundMode = Key.F8;

    /// <summary>F9: the editor's binding list. F11 is the editor's spare slot.</summary>
    public const Key EditorHelp = Key.F9;

    /// <summary>F10: the editor's level shaders on or off.</summary>
    public const Key EditorShaders = Key.F10;
}
