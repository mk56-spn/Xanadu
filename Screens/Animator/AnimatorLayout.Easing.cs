using Friflo.Engine.ECS;
using Xanadu.Model.Animator.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Animator;

/// <summary>
/// The timeline header's EASING button. It opens the shared ease picker
/// (<see cref="EasePickerPanel"/>), which the animator hosts
/// through <see cref="AnimatorEasePickerHost"/>.
/// </summary>
public static partial class AnimatorLayout
{
    public const float EasingButtonWidth = 150f;

    /// <summary>The header button that opens the picker and names the selected keys' curve.</summary>
    private static Entity EasingButton(UiBuilder ui) =>
        HeaderButton(ui, "EASING", new ToggleEasingWindow(), EasingButtonWidth);
}
