using Godot;
using Xanadu.Ecs;

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
        node.With(new SceneAction(target));

    /// <summary>Runs a scene action when the button is pressed.</summary>
    public static UiNode On(this UiNode node, SceneActionId action) =>
        node.With(new SceneCommand(action));
}

/// <summary>
/// The play HUD's vocabulary. HUD labels sit over the playfield and most are
/// rewritten by systems as the playhead moves, so they keep fixed boxes and a
/// dark backing; positions are layout data the HUD system can drag.
/// </summary>
public static class HudUi
{
    /// <summary>A HUD label in an explicit box, backed by the window colour.</summary>
    public static UiNode HudText(
        this UiNode parent,
        Vector2 center,
        Vector2 size,
        string text,
        int fontSize,
        Color color,
        Color? background = null) =>
        parent.Text(text, fontSize, color, TextAlign.Center)
            .Size(size)
            .At(center)
            .Fill(background ?? SceneUi.BackgroundColor);

    /// <summary>A HUD button in an explicit box.</summary>
    public static UiNode HudButton(
        this UiNode parent,
        Vector2 center,
        Vector2 size,
        string label,
        int fontSize = 18,
        Color? color = null) =>
        parent.Button(label, fontSize, color).Size(size).At(center);

    /// <summary>A flat HUD rectangle in an explicit box.</summary>
    public static UiNode HudRect(
        this UiNode parent,
        Vector2 center,
        Vector2 size,
        Color color) =>
        parent.Rect(color).Size(size).At(center);
}
