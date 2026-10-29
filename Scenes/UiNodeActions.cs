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
