using Xanadu.Ui.Systems.Input;

namespace Xanadu.Model.Settings;

/// <summary>What the playfield keys are: their order, which steer, their default keys and their names on screen.</summary>
public static class KeybindActions
{
    /// <summary>Every playfield key, directions first.</summary>
    public static readonly KeybindAction[] All =
    [
        KeybindAction.Left,
        KeybindAction.Right,
        KeybindAction.Up,
        KeybindAction.Down,
        KeybindAction.Action1,
        KeybindAction.Action2,
        KeybindAction.Action3,
    ];

    /// <summary>The three action keys, in bar order.</summary>
    public static readonly KeybindAction[] ActionKeys =
    [
        KeybindAction.Action1,
        KeybindAction.Action2,
        KeybindAction.Action3,
    ];

    /// <summary>Whether the key steers rather than hits.</summary>
    public static bool IsDirection(KeybindAction action) => action is
        KeybindAction.Left or KeybindAction.Right or KeybindAction.Up or KeybindAction.Down;

    /// <summary>The key an action is bound to until the player changes it.</summary>
    public static Key DefaultFor(KeybindAction action) => action switch
    {
        KeybindAction.Left => Key.F,
        KeybindAction.Right => Key.J,
        KeybindAction.Up => Key.K,
        KeybindAction.Down => Key.L,
        KeybindAction.Action1 => Key.S,
        KeybindAction.Action2 => Key.Space,
        KeybindAction.Action3 => Key.D,
        _ => Key.None,
    };

    /// <summary>The action's name on screen.</summary>
    public static string LabelFor(KeybindAction action) => action switch
    {
        KeybindAction.Left => "LEFT",
        KeybindAction.Right => "RIGHT",
        KeybindAction.Up => "UP",
        KeybindAction.Down => "DOWN",
        KeybindAction.Action1 => "ACTION 1",
        KeybindAction.Action2 => "ACTION 2",
        KeybindAction.Action3 => "ACTION 3",
        _ => "",
    };

    /// <summary>A key's name on screen.</summary>
    public static string KeyLabel(Key key) => key switch
    {
        Key.None => "—",
        Key.Space => "SPACE",
        _ => key.ToString().ToUpperInvariant(),
    };
}
