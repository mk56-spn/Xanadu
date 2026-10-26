using Friflo.Engine.ECS;
using Godot;

namespace Writer.Tools.Animator;

/// <summary>
/// How the preview maps skeleton space to the panel: a uniform zoom and a pan
/// offset in pixels. With the defaults the skeleton origin sits at the middle
/// of the panel, one unit is one pixel, which is what headless tests assume.
/// </summary>
public struct AnimatorPreviewView : IComponent
{
    public const float MinZoom = 0.2f;
    public const float MaxZoom = 8f;

    /// <summary>Zoom applied for one wheel notch.</summary>
    public const float WheelStep = 1.15f;

    public float Zoom;
    public Vector2 Pan;

    public static AnimatorPreviewView Default => new() { Zoom = 1f };

    /// <summary>Maps skeleton space to screen space for a preview panel of this area.</summary>
    public readonly Transform2D Placement(Rect2 area) => new(
        0f,
        new Vector2(Zoom, Zoom),
        0f,
        area.Position + area.Size / 2f + Pan);

    public readonly Vector2 ToScreen(Vector2 local, Rect2 area) => Placement(area) * local;

    public readonly Vector2 ToLocal(Vector2 screen, Rect2 area) =>
        Placement(area).AffineInverse() * screen;

    /// <summary>Zoom so the skeleton point under the pointer stays under it.</summary>
    public void ZoomAt(Vector2 screen, Rect2 area, float factor)
    {
        Vector2 local = ToLocal(screen, area);
        Zoom = Mathf.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Pan = screen - (area.Position + area.Size / 2f) - local * Zoom;
    }

    /// <summary>Frames a skeleton-space rectangle in the panel, centred.</summary>
    public void Fit(Rect2 bounds, Rect2 area)
    {
        Vector2 size = new(Mathf.Max(1f, bounds.Size.X), Mathf.Max(1f, bounds.Size.Y));
        Zoom = Mathf.Clamp(
            Mathf.Min(area.Size.X / size.X, area.Size.Y / size.Y),
            MinZoom,
            MaxZoom);
        Pan = -bounds.GetCenter() * Zoom;
    }
}
