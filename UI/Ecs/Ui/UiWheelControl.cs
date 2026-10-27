using Friflo.Engine.ECS;
using Godot;

namespace Xanadu.Ecs;

/// <summary>Reads and drives a wheel created by <see cref="UiBuilder.Wheel"/>.</summary>
public sealed class UiWheelControl(Entity wheel)
{
    public Entity Wheel { get; } = wheel;

    public int Selected => SelectedIndex(Wheel);

    public void Select(int index, bool raiseChanged = true) =>
        SetSelected(Wheel, index, raiseChanged);

    /// <summary>Selected wedge index, or -1 when the entity is not a wheel.</summary>
    public static int SelectedIndex(Entity wheel)
    {
        if (wheel.IsNull || !wheel.HasComponent<UiWheel>())
            return -1;

        return wheel.GetComponent<UiWheel>().Selected;
    }

    /// <summary>
    /// Selects a wedge index (wrapped into range). Returns false when the
    /// selection was already there. Consumers reflecting their own state into
    /// the wheel pass <paramref name="raiseChanged"/> as false so they do not
    /// echo their own change back as a new event.
    /// </summary>
    public static bool SetSelected(
        Entity wheel,
        int index,
        bool raiseChanged = true)
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
        if (raiseChanged)
            wheel.AddComponent(new UiWheelChanged(wrapped));
        return true;
    }

    public static string LabelAt(Entity wheel, int index)
    {
        if (wheel.IsNull || !wheel.HasComponent<UiWheel>())
            return "";

        UiWheel data = wheel.GetComponent<UiWheel>();
        if (data.Labels is not { Length: > 0 })
            return "";

        return data.Labels[UiWheelGeometry.Wrap(index, data.Labels.Length)];
    }
}
