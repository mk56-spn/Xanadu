using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Systems.Canvas;

namespace Writer.Tools.Animator.Systems;

/// <summary>
/// Draws the preview grid behind the SVG art, with its axes through the
/// skeleton origin. Redraws only when the preview panel moves or resizes.
/// </summary>
public sealed class AnimatorGridDrawSystem(IRectCanvas canvas, Rid target)
    : QuerySystem<AnimatorUi>
{
    /// <summary>Grid spacing in skeleton units (one unit is one pixel).</summary>
    public const float Cell = 20f;

    private Rect2 _drawn;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            Rect2 area = AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().Preview);
            if (area == _drawn)
                continue;

            _drawn = area;
            canvas.Clear(target);
            CanvasGrid.Draw(
                canvas,
                target,
                area,
                new Vector2(Cell, Cell),
                AnimatorGeometry.PreviewOrigin(area));
        }
    }
}
