using System.Collections.Generic;
using System.Linq;
using Xanadu.Core.Theme;
using Xanadu.Logic.Settings;
using Xanadu.Model.Settings;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.Settings;

/// <summary>
/// A second layout over the same logic: display options on the left as
/// segmented choices (every value its own button, the current one lit), the
/// keys on the right as key caps laid out like the hands that press them.
/// </summary>
public sealed class SplitSettingsLayout : IScreenLayout<SettingsLogic>
{
    private const float PanelWidth = 640;
    private const float SegmentWidth = 180;
    private const float CapSize = 132;

    private static readonly Flex Fill = new(Grow: 1, Cross: Cross.Stretch);

    /// <summary>Each lit-when-current button and the test that lights it.</summary>
    private readonly List<(Button Button, System.Func<SettingsLogic, bool> On)> _segments = new();
    private readonly List<bool?> _shown = new();
    private readonly Dictionary<KeybindAction, Button> _keys = new();

    public string Name => "SPLIT";

    public void Build(UiNode layer, SettingsLogic logic)
    {
        layer.Rect(SceneUi.BackgroundColor).Cover().Below();
        layer.Page().Pad(Space.Xxl).Gap(Space.Xl).Add(
            new Row(gap: Space.Lg)
            {
                new Button(SettingsStrings.Back, tone: Tone.Secondary, cmd: Cmd(SettingsEntrance.Leave), height: Control.Lg),
                new Label(SettingsStrings.Title, UiType.Title, ink: Ink.Accent),
            },
            new Row(Fill, gap: Space.Xxl)
            {
                new Column(width: PanelWidth, pad: Space.Xl, gap: Space.Lg, tone: Tone.Solid)
                {
                    new Label(SettingsStrings.Display, UiType.Section, ink: Ink.Accent),
                    Group(SettingsStrings.VSync,
                        Segment(SettingsStrings.On, SettingsEntrance.VSync, 1, l => l.VSync),
                        Segment(SettingsStrings.Off, SettingsEntrance.VSync, 0, l => !l.VSync)),
                    Group(SettingsStrings.WindowMode,
                        new[] { WindowMode.Windowed, WindowMode.Fullscreen, WindowMode.Borderless }
                            .Select(mode => Segment(SettingsText.WindowMode(mode), SettingsEntrance.WindowMode, (int)mode,
                                l => l.WindowMode == mode)).ToArray()),
                    Group(SettingsStrings.Autosave,
                        Segment(SettingsStrings.On, SettingsEntrance.Autosave, 1, l => l.Autosave),
                        Segment(SettingsStrings.Off, SettingsEntrance.Autosave, 0, l => !l.Autosave)),
                },
                new Column(Fill, pad: Space.Xl, gap: Space.Lg, tone: Tone.Solid)
                {
                    new Label(SettingsStrings.Keybinds, UiType.Section, ink: Ink.Accent),
                    new Row(gap: Space.Md) { logic.Actions.Where(KeybindActions.IsDirection).Select(Cap) },
                    new Row(gap: Space.Md) { logic.Actions.Where(a => !KeybindActions.IsDirection(a)).Select(Cap) },
                    new Spacer(),
                    new Label(SettingsStrings.Footer, UiType.Small, ink: Ink.Dim),
                },
            });
    }

    public void Refresh(SettingsLogic logic)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            bool on = _segments[i].On(logic);
            if (_shown[i] == on)
                continue;
            _shown[i] = on;
            _segments[i].Button.Node.Chosen(on);
        }

        foreach ((KeybindAction action, Button key) in _keys)
            key.Node.SetText(SettingsText.Key(logic, action));
    }

    /// <summary>A setting's name over its row of choices.</summary>
    private static Column Group(string label, params Button[] choices) => new(gap: Space.Sm)
    {
        new Label(label, UiType.Caption, ink: Ink.Dim),
        new Row(gap: Space.Xs) { choices },
    };

    /// <summary>One value of a setting: sends <paramref name="intent"/> with it, lit while <paramref name="on"/> holds.</summary>
    private Button Segment(string text, SettingsEntrance intent, int arg, System.Func<SettingsLogic, bool> on)
    {
        var button = new Button(text, UiType.Small, tone: Tone.Secondary, cmd: Cmd(intent, arg),
            width: SegmentWidth, height: Control.Md);
        _segments.Add((button, on));
        _shown.Add(null);
        return button;
    }

    /// <summary>A key cap: the binding big, what it does small underneath.</summary>
    private UiSpec Cap(KeybindAction action) => new Column(gap: Space.Xs, cross: Cross.Center)
    {
        (_keys[action] = new Button("", UiType.Heading, tone: Tone.Secondary, cmd: Cmd(SettingsEntrance.Rebind, (int)action),
            width: CapSize, height: CapSize)),
        new Label(KeybindActions.LabelFor(action), UiType.Caption, ink: Ink.Dim),
    };
}
