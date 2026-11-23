using Xanadu.Core.Theme;
using Xanadu.Logic.MainMenu;
using Xanadu.Screens.Shared;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using static Xanadu.Core.Theme.UiMetrics;
using static Xanadu.Ui.Ecs.Ui.Tree.UiSpecs;

namespace Xanadu.Screens.MainMenu;

/// <summary>
/// The title screen: the title at the top, the button block in the middle on a
/// backing so it reads over the moving notes, and the footer at the bottom.
/// </summary>
public sealed class MainMenuLayout : IScreenLayout<MainMenuLogic>
{
    private const float MenuWidth = 360;
    private const float PlayHeight = 66;
    private const float MenuHeight = 56;
    private const float LastHeight = 50;

    public string Name => "CLASSIC";

    public void Build(UiNode layer, MainMenuLogic logic)
    {
        layer.Theme(UiThemes.Menu);
        layer.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Spacer(),
            new Label(MainMenuStrings.Title, UiType.Display, ink: Ink.Accent),
            new Label(MainMenuStrings.Subtitle, UiType.Label, ink: Ink.Dim),
            new Spacer(),
            new Column(gap: Space.Md, pad: SceneUi.BackingPad, tone: Tone.Backing)
            {
                Entry(MainMenuStrings.Play, UiType.Section, Tone.Primary, PlayHeight, MainMenuEntrance.Play),
                Entry(MainMenuStrings.Editor, UiType.Label, Tone.Secondary, MenuHeight, MainMenuEntrance.Editor),
                Entry(MainMenuStrings.Settings, UiType.Label, Tone.Secondary, MenuHeight, MainMenuEntrance.Settings),
                Entry(MainMenuStrings.OffsetWizard, UiType.Small, Tone.Secondary, LastHeight, MainMenuEntrance.OffsetWizard),
                Entry(MainMenuStrings.Profiles, UiType.Small, Tone.Secondary, LastHeight, MainMenuEntrance.Profiles),
                // A feel test: a library song with the background pulsing on its beats.
                Entry(MainMenuStrings.Bench, UiType.Small, Tone.Secondary, LastHeight, MainMenuEntrance.Bench),
            },
            // The block sits high: most of the free room goes below it.
            new Spacer(4),
            new Label(MainMenuStrings.Footer, UiType.Small, ink: Ink.Dim));
    }

    /// <summary>A full-width menu button for <paramref name="entrance"/>.</summary>
    private static Button Entry(LString label, int type, Tone tone, float height, MainMenuEntrance entrance) =>
        new(label, type, tone: tone, cmd: Cmd(entrance), width: MenuWidth, height: height);
}
