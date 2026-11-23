using Xanadu.Core.Keys;
using Xanadu.Storage.Settings;
using System.Collections.Generic;
using System.Linq;
using Xanadu.Logic.Screens;
using Xanadu.Model.Settings;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Settings;

/// <summary>
/// The settings screen's entrances. For the values, an argument of
/// <see cref="SettingsLogic.Flip"/> toggles or cycles; any other sets it
/// (0/1, or a <see cref="Ui.Systems.WindowMode"/>). Rebind takes a <see cref="KeybindAction"/>.
/// </summary>
public enum SettingsEntrance
{
    Leave,
    VSync,
    WindowMode,
    Autosave,
    Rebind,
}

/// <summary>
/// The settings screen's behaviour with no picture of it: the values a layout
/// shows and the entrances it binds. It knows nothing about how it is drawn,
/// so any number of layouts can sit on top of it.
/// </summary>
public sealed class SettingsLogic(KeybindSettings keybinds, DisplaySettings display, IScreenNavigator navigator)
    : IScreenLogic<SettingsEntrance>
{
    /// <summary>The argument that toggles a switch or steps a choice on to its next value.</summary>
    public const int Flip = -1;

    public ScreenContract<SettingsEntrance> Contract { get; } = new ScreenContract<SettingsEntrance>()
        .Mandatory(SettingsEntrance.Leave)
        .Mandatory(SettingsEntrance.VSync)
        .Mandatory(SettingsEntrance.WindowMode)
        .Mandatory(SettingsEntrance.Rebind, KeybindActions.All.Select(a => (int)a).ToArray(),
            a => ((KeybindAction)a).ToString())
        // Only mappers care; a player-facing skin may leave it out.
        .Optional(SettingsEntrance.Autosave);

    public bool VSync => display.VSyncEnabled;

    public WindowMode WindowMode => display.WindowMode;

    public bool Autosave => display.EditorAutosave;

    /// <summary>The rebindable playfield keys, in screen order.</summary>
    public IReadOnlyList<KeybindAction> Actions => KeybindActions.All;

    public Key KeyFor(KeybindAction action) => keybinds.Get(action);

    /// <summary>The key waiting for its new binding, if any.</summary>
    public KeybindAction? Capturing => keybinds.Capturing;

    public void Do(SettingsEntrance entrance, int arg)
    {
        switch (entrance)
        {
            case SettingsEntrance.VSync:
                display.SetVSync(arg == Flip ? !display.VSyncEnabled : arg != 0);
                break;
            case SettingsEntrance.WindowMode:
                display.SetWindowMode(arg == Flip ? (WindowMode)(((int)display.WindowMode + 1) % 3) : (WindowMode)arg);
                break;
            case SettingsEntrance.Autosave:
                display.SetEditorAutosave(arg == Flip ? !display.EditorAutosave : arg != 0);
                break;
            case SettingsEntrance.Rebind:
                keybinds.BeginCapture((KeybindAction)arg);
                break;
            case SettingsEntrance.Leave:
                navigator.Back();
                break;
        }
    }

    public void Close() => keybinds.CancelCapture();
}
