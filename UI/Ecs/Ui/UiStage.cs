using Godot;

namespace Xanadu.Ecs;

/// <summary>
/// The frame a screen is authored against: a fixed 1920x1080 design space that
/// the host scales as one 16:9 surface. A scene asks the stage for its centre
/// and bounds instead of reading the window, so there is no per-scene size math
/// and no resize path to maintain.
/// <para>
/// 16:9 only for now. <see cref="Size"/> equals <see cref="Design"/> today;
/// when other aspects are supported, the stage grows and scenes anchored to its
/// bounds follow without touching their authoring numbers.
/// </para>
/// </summary>
public readonly record struct UiStage(Vector2 Design, Vector2 Size, float Scale)
{
    /// <summary>The design space every scene is authored in.</summary>
    public static readonly Vector2 Design1080 = new(1920, 1080);

    /// <summary>The stage as it stands today: design size, scale 1.</summary>
    public static readonly UiStage Full = new(Design1080, Design1080, 1f);

    /// <summary>Width of the design space.</summary>
    public float DesignWidth => Design.X;

    /// <summary>Height of the design space.</summary>
    public float DesignHeight => Design.Y;

    /// <summary>The stage's width in design units.</summary>
    public float Width => Size.X;

    /// <summary>The stage's height in design units.</summary>
    public float Height => Size.Y;

    /// <summary>The middle of the stage, where a centred panel sits.</summary>
    public Vector2 Center => Size / 2;

    /// <summary>The whole stage as a rectangle.</summary>
    public Rect2 Bounds => new(Vector2.Zero, Size);
}
