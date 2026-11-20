using Friflo.Engine.ECS;
using Xanadu.Core.Theme;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Screens.Shared;
using static Xanadu.Core.Theme.UiMetrics;

namespace Xanadu.Screens.MainMenu;

/// <summary>
/// Title screen with play, editor and settings entry points, as a spec tree:
/// the title at the top, the button block in the middle on a backing so it
/// reads over the moving notes, and the footer at the bottom.
/// </summary>
public static class MainMenuScene
{
    private const float MenuWidth = 360;
    private const float PlayHeight = 66;
    private const float MenuHeight = 56;
    private const float LastHeight = 50;

    public static void Build(
        EntityStore entities,
        Entity root,
        IVisualItemFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, root, UiLayers.Menu, visuals, UiStage.Full)
            .Theme(UiThemes.Menu);

        ui.Page().Cross(Cross.Center).Pad(Space.Xxl).Gap(Space.Lg).Add(
            new Spacer(),
            new Label(MainMenuStrings.Title, UiType.Display, ink: Ink.Accent),
            new Label(MainMenuStrings.Subtitle, UiType.Label, ink: Ink.Dim),
            new Spacer(),
            new Column(gap: Space.Md, pad: SceneUi.BackingPad, tone: Tone.Backing)
            {
                Entry(MainMenuStrings.Play, UiType.Section, Tone.Primary, PlayHeight, SceneKind.SongSelect),
                Entry(MainMenuStrings.Editor, UiType.Label, Tone.Secondary, MenuHeight, SceneKind.Editor),
                Entry(MainMenuStrings.Settings, UiType.Label, Tone.Secondary, MenuHeight, SceneKind.Settings),
                Entry(MainMenuStrings.OffsetWizard, UiType.Small, Tone.Secondary, LastHeight, SceneKind.OffsetWizard),
                Entry(MainMenuStrings.Profiles, UiType.Small, Tone.Secondary, LastHeight, SceneKind.Profiles),
                // A feel test: a library song with the background pulsing on its beats.
                Entry(MainMenuStrings.Bench, UiType.Small, Tone.Secondary, LastHeight, SceneKind.Bench),
            },
            // The block sits high: most of the free room goes below it.
            new Spacer(4),
            new Label(MainMenuStrings.Footer, UiType.Small, ink: Ink.Dim));
    }

    /// <summary>A full-width menu button going to <paramref name="scene"/>.</summary>
    private static Button Entry(LString label, int type, Tone tone, float height, SceneKind scene) =>
        new(label, type, tone: tone, width: MenuWidth, height: height) { Style = n => n.On(scene) };
}
