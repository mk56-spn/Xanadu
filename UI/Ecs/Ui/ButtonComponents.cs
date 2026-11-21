using Friflo.Engine.ECS;
using Xanadu.Core.Maths;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A hoverable, clickable button's colours. Hover and the click flash are tween
/// layers over <see cref="Normal"/> (<see cref="UiTweenKeys.Hover"/>,
/// <see cref="UiTweenKeys.Flash"/>); read how far hover is with
/// <see cref="UiTween.Weight"/>.
/// </summary>
public struct ButtonVisual(Color normal, Color hover, Color flash) : IComponent
{
    public Color Normal = normal;
    public Color Hover = hover;
    public Color Flash = flash;
    public bool WasPressed;

    public static ButtonVisual Create(Color normal) => new(
        normal,
        normal.Lerp(Colors.White, 0.16f),
        Colors.White);
}
