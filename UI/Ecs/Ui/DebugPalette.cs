using Godot;
using Xanadu.Theme;

namespace Xanadu.Ecs;

/// <summary>
/// Every colour the debug views use, wherever they live: the F3 stats, the F4 bounds
/// and tree, the F8 shader tuner, the F10 hitboxes and the F1 frame rate. Add a colour here, not in a view.
/// </summary>
public static class DebugPalette
{
    // Panels.
    public static readonly Color Panel = new(0.02f, 0.03f, 0.06f, 0.94f);
    public static readonly Color PanelBorder = new(0.45f, 0.6f, 0.85f, 0.8f);
    public static readonly Color Title = new("#ffc035");
    public static readonly Color Header = new(0.55f, 0.7f, 1f);
    public static readonly Color Text = new(0.85f, 0.9f, 1f);
    public static readonly Color TextDim = new(0.55f, 0.6f, 0.7f);
    public static readonly Color Selection = new(0.2f, 0.32f, 0.55f, 0.55f);
    public static readonly Color Warning = new(1f, 0.45f, 0.4f);

    // Frame rate: green at the target, through yellow and orange, to red at 60.
    public static readonly Color FpsGreen = Palette.Success;
    public static readonly Color FpsYellow = new("#facc15");
    public static readonly Color FpsOrange = Palette.Warning;
    public static readonly Color FpsRed = new("#ef4444");

    // Controls.
    public static readonly Color Control = Palette.Muted;
    public static readonly Color ControlText = Colors.White;

    // UI bounds.
    public static readonly Color ButtonFill = new(1f, 0.2f, 0.2f, 0.16f);
    public static readonly Color ButtonBorder = new(1f, 0.3f, 0.3f, 0.8f);
    public static readonly Color ContainerFill = new(1f, 0.8f, 0.2f, 0.10f);
    public static readonly Color ContainerBorder = new(1f, 0.85f, 0.25f, 0.7f);
    public static readonly Color SelectedFill = new(0.2f, 0.9f, 1f, 0.3f);
    public static readonly Color SelectedBorder = new(0.3f, 0.95f, 1f, 1f);
    public static readonly Color Pin = new(0.3f, 1f, 0.45f, 0.95f);
    public static readonly Color PinOutline = new(0.05f, 0.2f, 0.08f, 0.95f);

    // Playfield hitboxes.
    public static readonly Color PlayerHitbox = Palette.Success;
    public static readonly Color BlockHitbox = Palette.Rose;
    public static readonly Color SpikeHitbox = Palette.Warning;
    public static readonly Color NoteReach = Palette.Accent;
    public static readonly Color WeaponHitbox = Palette.Info;
    public static readonly Color HitboxFill = new(1f, 1f, 1f, 0.1f);

    /// <summary>A soft version of a text colour, for the glow behind it.</summary>
    public static Color Glow(Color color) => new(color, 0.5f);

    /// <summary>The colour of a frame rate: pure green from 240 up, pure red from 60 down.</summary>
    public static Color Fps(double fps)
    {
        const float red = 60f;
        const float orange = 120f;
        const float yellow = 180f;
        const float green = 240f;

        float value = (float)fps;
        if (value <= red)
            return FpsRed;
        if (value < orange)
            return FpsRed.Lerp(FpsOrange, (value - red) / (orange - red));
        if (value < yellow)
            return FpsOrange.Lerp(FpsYellow, (value - orange) / (yellow - orange));
        if (value < green)
            return FpsYellow.Lerp(FpsGreen, (value - yellow) / (green - yellow));
        return FpsGreen;
    }
}
