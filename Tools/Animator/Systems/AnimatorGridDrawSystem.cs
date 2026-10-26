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
    private float _drawnZoom;
    private Vector2 _drawnPan;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            Rect2 area = AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().Preview);
            AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
            if (area == _drawn && view.Zoom == _drawnZoom && view.Pan == _drawnPan)
                continue;

            _drawn = area;
            _drawnZoom = view.Zoom;
            _drawnPan = view.Pan;
            canvas.Clear(target);
            CanvasGrid.Draw(
                canvas,
                target,
                area,
                new Vector2(Cell, Cell) * view.Zoom,
                AnimatorGeometry.PreviewOrigin(area, view));
        }
    }
}
