using System;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Strings;
using Xanadu.Theme;
using Xanadu.Ui.Tree;
using static Xanadu.Theme.UiMetrics;
using static Xanadu.Ui.Tree.UiSpecs;

namespace Xanadu.Scenes;

/// <summary>
/// Settings with rebindable playfield keys, as a spec tree: the title and the
/// card flow down the page, and every setting (keybinds too) is a label and its
/// control in a row. The card sizes to its contents; no screen size is read here.
/// </summary>
public static class SettingsScene
{
    private const float CardWidth = 760;

    /// <summary>Width of the label column and of the controls beside it.</summary>
    private const float LabelColumn = 300;
    private const float ControlWidth = 240;
    private const float ColumnGap = 64;
    private const float BackWidth = 150;

    public static void Build(
        EntityStore entities,
        Entity root,
        KeybindSettings keybinds,
        DisplaySettings display,
        IVisualRidFactory? visuals = null)
    {
        var ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, UiStage.Full);
        ui.Rect(SceneUi.BackgroundColor).Cover().Below();

        ui.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label(Text(SettingsStrings.Title), UiType.Title, ink: Ink.Accent),
            new Spacer(),
            new Column(width: CardWidth, pad: Space.Xxl, gap: Space.Lg, cross: Cross.Center, tone: Tone.Solid)
            {
                Toggle(SettingsStrings.VSync, SettingsStrings.VSyncOn, SettingsStrings.VSyncOff,
                    display.VSyncEnabled, display.SetVSync),
                Setting(Text(SettingsStrings.WindowMode),
                    Choice(WindowModeLabel(display.WindowMode), style: n => n.With(new WindowModeButton()))),
                Toggle(SettingsStrings.Autosave, SettingsStrings.AutosaveOn, SettingsStrings.AutosaveOff,
                    display.EditorAutosave, display.SetEditorAutosave),
                new Label(Text(SettingsStrings.Keybinds), UiType.Section, ink: Ink.Accent),
                new Column(gap: Space.Xs)
                {
                    new Row(gap: ColumnGap) { Header(SettingsStrings.Action, LabelColumn), Header(SettingsStrings.Key, ControlWidth) },
                    KeybindSettings.Actions.Select(action => Setting(KeybindSettings.LabelFor(action),
                        Choice(KeybindSettings.KeyLabel(keybinds.Get(action)), Cmd(action)))),
                },
                new Label(Text(SettingsStrings.Footer), UiType.Small, ink: Ink.Dim),
            },
            new Spacer(),
            new Button(Text(SettingsStrings.Back), tone: Tone.Secondary, width: BackWidth, height: Control.Lg)
            {
                Style = n => n.On(SceneKind.Main).Anchor(UiEdge.TopLeft, Space.Xl),
            });
    }

    public static string WindowModeLabel(Systems.WindowMode mode) => Text(mode switch
    {
        Systems.WindowMode.Fullscreen => SettingsStrings.Fullscreen,
        Systems.WindowMode.Borderless => SettingsStrings.Borderless,
        _ => SettingsStrings.Windowed,
    });

    /// <summary>A setting's label in the left column, its control beside it.</summary>
    private static Row Setting(string label, UiSpec control) => new(gap: ColumnGap)
    {
        new Label(label, UiType.Label, width: LabelColumn, height: Control.Md),
        control,
    };

    /// <summary>A setting's button in the right column, doing <paramref name="cmd"/> or what <paramref name="style"/> gives it.</summary>
    private static Button Choice(string text, UiCommand? cmd = null, Action<UiNode>? style = null) =>
        new(text, UiType.Small, tone: Tone.Secondary, cmd: cmd, width: ControlWidth, height: Control.Md) { Style = style };

    /// <summary>A setting switched on and off; <paramref name="changed"/> hears each flip.</summary>
    private static Row Toggle(LString label, LString onText, LString offText, bool value, Action<bool> changed)
    {
        string on = Text(onText), off = Text(offText);
        return Setting(Text(label), Choice(value ? on : off, style: n =>
            n.With(new Toggle(on, off, value, n.Roles.Chosen.Fill, n.Roles.Secondary.Fill))
                .Entity.OnToggled(changed)));
    }

    private static Label Header(LString label, float width) =>
        new(Text(label), UiType.Caption, ink: Ink.Dim, width: width, height: Control.Xs);

    private static string Text(LString text) => LocalizedText.Get(text);
}
