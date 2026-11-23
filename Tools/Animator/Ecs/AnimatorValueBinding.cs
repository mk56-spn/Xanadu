using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator.Ecs;

/// <summary>What kind of value an animator value field edits.</summary>
public enum AnimatorFieldKind
{
    /// <summary>One of the <see cref="AnimatorSliderValue"/> numbers, named by <see cref="AnimatorValueBinding.A"/>.</summary>
    Slider,

    /// <summary>The selection's translation (x, y).</summary>
    Position,

    /// <summary>The selection's rotation, in degrees.</summary>
    Rotation,

    /// <summary>The selection's scale (x, y).</summary>
    Scale,

    /// <summary>The selected part's draw layer.</summary>
    Layer,

    /// <summary>Settings of the selected chain: <see cref="AnimatorValueBinding.A"/>, and <see cref="AnimatorValueBinding.B"/> for a pair.</summary>
    Chain,

    /// <summary>Settings of the selected cloth, as for a chain.</summary>
    Cloth,

    /// <summary>The selected cloth's fill colour.</summary>

    /// <summary>The selected part's self modulate: its art is multiplied by it.</summary>
    PartColour,

    /// <summary>The selected clip event's time, in seconds.</summary>
    EventTime,
}

/// <summary>
/// Binds a value field (<see cref="Xanadu.Ui.Ecs.Ui.UiValueField"/>) to what it
/// edits in the rig. <see cref="Systems.AnimatorValueFieldSystem"/> keeps the
/// field on that value and turns the user's edits into undo steps.
/// </summary>
public struct AnimatorValueBinding(AnimatorFieldKind kind, int a = 0, int b = -1) : IComponent
{
    public AnimatorFieldKind Kind = kind;

    /// <summary>The first value's setting (a slider value, chain or cloth setting).</summary>
    public int A = a;

    /// <summary>The second value's setting for a pair, or -1.</summary>
    public int B = b;

    public static AnimatorValueBinding Of(AnimatorSliderValue value) => new(AnimatorFieldKind.Slider, (int)value);
}
