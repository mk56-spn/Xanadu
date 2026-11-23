using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A colour swatch that can be picked up: press it and drag, and its fill
/// colour goes with the pointer to whatever <see cref="UiColorDrop"/> it is
/// let go over. A press that does not move is left to whatever else the
/// swatch does.
/// </summary>
public struct UiColorSwatch : ITag;

/// <summary>
/// Somewhere a dragged colour can be dropped. The drag system emits
/// <see cref="ColorDropped"/> on it; the owner listens with
/// <see cref="UiColorDrops.OnColorDropped"/> and applies it as if it had been typed or picked.
/// </summary>
public struct UiColorDrop : ITag;

/// <summary>Signal: a dragged colour was let go over this <see cref="UiColorDrop"/>.</summary>
public readonly struct ColorDropped(Color color)
{
    public readonly Color Color = color;
}

/// <summary>Listening to colour drops through the entity's native signals.</summary>
public static class UiColorDrops
{
    /// <summary>Calls <paramref name="dropped"/> with each colour let go over <paramref name="target"/>.</summary>
    public static Entity OnColorDropped(this Entity target, System.Action<Color> dropped)
    {
        target.AddSignalHandler<ColorDropped>(signal => dropped(signal.Event.Color));
        return target;
    }
}
