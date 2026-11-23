using Xanadu.Logic.Settings;
using Xanadu.Model.Settings;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Systems;

namespace Xanadu.Screens.Settings;

/// <summary>How the settings' values read on screen, shared by every layout.</summary>
public static class SettingsText
{
    public static string WindowMode(WindowMode mode) => LocalizedText.Get(mode switch
    {
        Ui.Systems.WindowMode.Fullscreen => SettingsStrings.Fullscreen,
        Ui.Systems.WindowMode.Borderless => SettingsStrings.Borderless,
        _ => SettingsStrings.Windowed,
    });

    /// <summary>A key's binding, or the capture prompt while it waits for one.</summary>
    public static string Key(SettingsLogic logic, KeybindAction action) =>
        logic.Capturing == action
            ? LocalizedText.Get(SettingsStrings.PressKey)
            : KeybindActions.KeyLabel(logic.KeyFor(action));

    public static string OnOff(bool on, LString onText, LString offText) =>
        LocalizedText.Get(on ? onText : offText);
}
