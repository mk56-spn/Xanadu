using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>
/// Scene-level actions as chainable calls, so a button reads as one sentence:
/// <c>ui.Button(label, size).On(SceneKind.Main)</c>. This replaces the
/// SceneUi.Button / ButtonEntity / CarouselButton family.
/// </summary>
public static class UiNodeActions
{
    /// <summary>Navigates to a scene when the button is pressed.</summary>
    public static UiNode On(this UiNode node, SceneKind target) =>
        node.With(SceneAction.Go(target));

    /// <summary>Runs a scene action when the button is pressed.</summary>
    public static UiNode On(this UiNode node, SceneActionId action) =>
        node.With(UiCommand.Of(action));
}

/// <summary>
/// The play HUD's vocabulary. HUD labels sit over the playfield and most are
/// rewritten by systems as the playhead moves, so they keep fixed boxes and a
/// dark backing; positions are layout data the HUD system can drag.
/// </summary>
public static class HudUi
{
    /// <summary>A HUD label in an explicit box, on its theme's backing unless given a background.</summary>
    public static UiNode HudText(
        this UiNode parent,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        Color? background = null) =>
        background is { } fill
            ? parent.Text(text, fontSize, color, TextAlign.Center).Size(size).At(center).Fill(fill)
            : parent.Text(text, fontSize, color, TextAlign.Center).Size(size).At(center).Backing();

    /// <summary>A HUD button in an explicit box.</summary>
    public static UiNode HudButton(
        this UiNode parent,
        Vector2 center,
        Vector2 size,
        string label,
        int fontSize = 18,
        Color? color = null) =>
        parent.Button(label, fontSize, color).Size(size).At(center);

    /// <summary>
    /// The theme's backing filling <paramref name="area"/>, behind a HUD group that
    /// has none of its own. Build it before the group's labels so it draws under
    /// them, and add it to the group so dragging the group moves it too.
    /// </summary>
    public static Entity HudBacking(this UiNode parent, Rect2 area) =>
        parent.Rect(default).Size(area.Size).At(area.GetCenter()).Backing().Entity;

    /// <summary>A flat HUD rectangle in an explicit box.</summary>
    public static UiNode HudRect(
        this UiNode parent,
        Vector2 center,
        Vector2 size,
        Color color) =>
        parent.Rect(color).Size(size).At(center);
}
