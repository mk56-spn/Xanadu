using Writer.Ecs;

namespace Writer.Editor;

/// <summary>
/// The dash-direction configuration of the generic Writer.UI wheel. The order
/// is clockwise from straight up, which is also the wheel's default winding.
/// </summary>
public static class DashDirectionWheel
{
    public static readonly DashDirection[] Directions =
    {
        DashDirection.Up,
        DashDirection.UpRight,
        DashDirection.Right,
        DashDirection.DownRight,
        DashDirection.Down,
        DashDirection.DownLeft,
        DashDirection.Left,
        DashDirection.UpLeft,
    };

    public static readonly string[] Labels =
    {
        "↑",
        "↗",
        "→",
        "↘",
        "↓",
        "↙",
        "←",
        "↖",
    };

    public static int IndexOf(DashDirection direction)
    {
        for (int i = 0; i < Directions.Length; i++)
        {
            if (Directions[i] == direction)
                return i;
        }

        return 0;
    }

    public static DashDirection At(int index) =>
        Directions[UiWheelGeometry.Wrap(index, Directions.Length)];
}
