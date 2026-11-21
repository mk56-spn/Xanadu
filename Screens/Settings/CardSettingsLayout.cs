using System.Collections.Generic;
using System.Linq;
using Xanadu.Core.Theme;
using Xanadu.Logic.Settings;
using Xanadu.Model.Settings;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.Settings;

/// <summary>
/// The original layout: one centred card, every setting a label with one
/// button beside it that flips or cycles the value, the keys in a list below.
/// </summary>
public sealed class CardSettingsLayout : IScreenLayout<SettingsLogic>
{
    private const float CardWidth = 760;
    private const float LabelColumn = 300;
    private const float ControlWidth = 240;
    private const float ColumnGap = 64;
    private const float BackWidth = 150;

    private Button _vsync = null!, _window = null!, _autosave = null!;
    private readonly Dictionary<KeybindAction, Button> _keys = new();

    public string Name => "CARD";

    public void Build(UiNode layer, SettingsLogic logic)
    {
        layer.Rect(SceneUi.BackgroundColor).Cover().Below();
        layer.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Label(SettingsStrings.Title, UiType.Title, ink: Ink.Accent),
            new Spacer(),
            new Column(width: CardWidth, pad: Space.Xxl, gap: Space.Lg, cross: Cross.Center, tone: Tone.Solid)
            {
                Setting(SettingsStrings.VSync, _vsync = Choice(Cmd(SettingsEntrance.VSync, SettingsLogic.Flip))),
                Setting(SettingsStrings.WindowMode, _window = Choice(Cmd(SettingsEntrance.WindowMode, SettingsLogic.Flip))),
                Setting(SettingsStrings.Autosave, _autosave = Choice(Cmd(SettingsEntrance.Autosave, SettingsLogic.Flip))),
                new Label(SettingsStrings.Keybinds, UiType.Section, ink: Ink.Accent),
                new Column(gap: Space.Xs)
                {
                    new Row(gap: ColumnGap) { Header(SettingsStrings.Action, LabelColumn), Header(SettingsStrings.Key, ControlWidth) },
                    logic.Actions.Select(action => Setting(KeybindActions.LabelFor(action),
                        _keys[action] = Choice(Cmd(SettingsEntrance.Rebind, (int)action)))),
                },
                new Label(SettingsStrings.Footer, UiType.Small, ink: Ink.Dim),
            },
            new Spacer(),
            new Button(SettingsStrings.Back, tone: Tone.Secondary, cmd: Cmd(SettingsEntrance.Leave), width: BackWidth, height: Control.Lg)
            {
                Style = n => n.Anchor(UiEdge.TopLeft, Space.Xl),
            });
    }

    public void Refresh(SettingsLogic logic)
    {
        _vsync.Node.SetText(SettingsText.OnOff(logic.VSync, SettingsStrings.VSyncOn, SettingsStrings.VSyncOff));
        _window.Node.SetText(SettingsText.WindowMode(logic.WindowMode));
        _autosave.Node.SetText(SettingsText.OnOff(logic.Autosave, SettingsStrings.AutosaveOn, SettingsStrings.AutosaveOff));
        foreach ((KeybindAction action, Button key) in _keys)
            key.Node.SetText(SettingsText.Key(logic, action));
    }

    /// <summary>A setting's label in the left column, its control beside it.</summary>
    private static Row Setting(string label, UiSpec control) => new(gap: ColumnGap)
    {
        new Label(label, UiType.Label, width: LabelColumn, height: Control.Md),
        control,
    };

    /// <summary>A control in the right column; <see cref="Refresh"/> writes its text.</summary>
    private static Button Choice(UiCommand cmd) =>
        new("", UiType.Small, tone: Tone.Secondary, cmd: cmd, width: ControlWidth, height: Control.Md);

    private static Label Header(LString label, float width) =>
        new(label, UiType.Caption, ink: Ink.Dim, width: width, height: Control.Xs);
}
