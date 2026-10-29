using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Settings;
using Xanadu.Strings;
using Xanadu.Theme;

namespace Xanadu.Scenes;

/// <summary>
/// Settings scene with rebindable playfield keys. Built on the design stage:
/// the card is one column, the keybinds a two-column grid, and the footer is
/// anchored to the card's bottom edge. No screen size is read here.
/// </summary>
public static class SettingsScene
{
    private const float CardWidth = 760;
    private const float CardMaxHeight = 600;
    private const float CardMinHeight = 500;
    private const float CardViewportInset = 120;

    /// <summary>How far below the stage's middle the card starts.</summary>
    private const float CardDrop = 40;

    /// <summary>The title's baseline sits above the card, in design pixels.</summary>
    private const float TitleY = 90;

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
        UiStage stage = UiStage.Full;
        Rect2 card = Card(stage);

        // The backdrop and the card's fill sit a layer below the content so the
        // content can never lose sibling draw order against them.
        UiNode backdrop = UiNode.Layer(
            entities, root, SceneUi.MenuLayer - 1, visuals, stage);
        backdrop.Rect(SceneUi.BackgroundColor).Cover().Below();
        backdrop.Rect(SceneUi.PanelColor).Box(card);

        UiNode ui = UiNode.Layer(entities, root, SceneUi.MenuLayer, visuals, stage);
        ui.Text(LocalizedText.Get(SettingsStrings.Title), UiType.Title, SceneUi.AccentColor)
            .At(new Vector2(stage.Center.X, TitleY));

        UiNode content = ui.Column()
            .Box(card)
            .Pad(UiMetrics.Space.Xxl)
            .Gap(UiMetrics.Space.Lg);

        VsyncRow(content, display);
        content.Text(
            LocalizedText.Get(SettingsStrings.Keybinds),
            UiType.Section,
            SceneUi.AccentColor);
        KeybindTable(content, keybinds);
        content.Edge(UiEdge.Bottom)
            .Pad(UiMetrics.Space.Md)
            .Text(
                LocalizedText.Get(SettingsStrings.Footer),
                UiType.Small,
                SceneUi.DimTextColor);

        ui.Button(
                LocalizedText.Get(SettingsStrings.Back),
                UiType.Label,
                SceneUi.MutedButtonColor)
            .Size(BackWidth, UiMetrics.Control.Lg)
            .At(new Vector2(
                UiMetrics.Space.Xl + BackWidth / 2,
                UiMetrics.Space.Xl + UiMetrics.Control.Lg / 2))
            .On(SceneKind.Main);
    }

    private static Rect2 Card(UiStage stage)
    {
        float height = Mathf.Min(
            CardMaxHeight,
            Mathf.Max(CardMinHeight, stage.Height - CardViewportInset));

        return new Rect2(
            new Vector2(
                (stage.Width - CardWidth) / 2,
                stage.Center.Y + CardDrop - height / 2),
            new Vector2(CardWidth, height));
    }

    /// <summary>One label and one switch, on the same grid as the keybinds.</summary>
    private static void VsyncRow(UiNode card, DisplaySettings display)
    {
        string on = LocalizedText.Get(SettingsStrings.VSyncOn);
        string off = LocalizedText.Get(SettingsStrings.VSyncOff);

        UiNode row = card.Row().Gap(ColumnGap);
        row.Text(LocalizedText.Get(SettingsStrings.VSync), UiType.Label, SceneUi.TextColor)
            .Size(LabelColumn, UiMetrics.Control.Md);
        row.Button(display.VSyncEnabled ? on : off, UiType.Small, SceneUi.MutedButtonColor)
            .Size(ControlWidth, UiMetrics.Control.Md)
            .With(new Toggle(on, off, display.VSyncEnabled, Palette.Primary,
                SceneUi.MutedButtonColor))
            .With(new VSyncToggle());
    }

    /// <summary>Every bindable action, two columns, one row each.</summary>
    private static void KeybindTable(UiNode card, KeybindSettings keybinds)
    {
        UiNode table = card.Grid(2, gap: ColumnGap, rowGap: UiMetrics.Space.Xs);
        ColumnHeader(table, SettingsStrings.Action, LabelColumn);
        ColumnHeader(table, SettingsStrings.Key, ControlWidth);

        foreach (KeybindAction action in KeybindSettings.Actions)
        {
            table.Text(KeybindSettings.LabelFor(action), UiType.Label, SceneUi.TextColor)
                .Size(LabelColumn, UiMetrics.Control.Md);
            table.Button(
                    KeybindSettings.KeyLabel(keybinds.Get(action)),
                    UiType.Small,
                    SceneUi.MutedButtonColor)
                .Size(ControlWidth, UiMetrics.Control.Md)
                .With(new KeybindButton(action));
        }
    }

    private static void ColumnHeader(UiNode table, LString label, float width) =>
        table.Text(LocalizedText.Get(label), UiType.Caption, SceneUi.DimTextColor)
            .Size(width, UiMetrics.Control.Xs)
            .Align(TextAlign.Left);
}
