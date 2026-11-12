using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>Reads and drives a wheel created by <see cref="UiBuilder.Wheel"/>.</summary>
public sealed class UiWheelControl(Entity wheel)
{
    public Entity Wheel { get; } = wheel;

    public int Selected => SelectedIndex(Wheel);

    public void Select(int index) => SetSelected(Wheel, index);

    /// <summary>Selected wedge index, or -1 when the entity is not a wheel.</summary>
    public static int SelectedIndex(Entity wheel)
    {
        if (wheel.IsNull || !wheel.HasComponent<UiWheel>())
            return -1;

        return wheel.GetComponent<UiWheel>().Selected;
    }

    /// <summary>
    /// Selects a wedge index (wrapped into range). Returns false when the
    /// selection was already there.
    /// </summary>
    public static bool SetSelected(Entity wheel, int index)
    {
        if (wheel.IsNull || !wheel.HasComponent<UiWheel>())
            return false;

        UiWheel data = wheel.GetComponent<UiWheel>();
        if (data.Labels is not { Length: > 0 })
            return false;

        int wrapped = UiWheelGeometry.Wrap(index, data.Labels.Length);
        if (data.Selected == wrapped)
            return false;

        wheel.GetComponent<UiWheel>().Selected = wrapped;
        wheel.AddTag<Dirty>();
        return true;
    }
}
