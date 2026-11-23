using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Model.Animator;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Animator;

/// <summary>
/// Draws the preview grid behind the art, with its axes through the
/// skeleton origin. Redraws only when the preview panel moves or resizes, or
/// when the shared <see cref="ViewportGrid"/> switch changes.
/// </summary>
public sealed class AnimatorGridDrawSystem(IRectCanvas canvas, Handle target)
    : QuerySystem<AnimatorUi>
{
    private Rect2 _drawn;
    private float _drawnZoom;
    private Vector2 _drawnPan;
    private bool _drawnVisible;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.Entities)
        {
            Rect2 area = AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().Preview);
            AnimatorPreviewView view = session.GetComponent<AnimatorPreviewView>();
            bool visible = ViewportGrid.IsVisible(session.Store);
            if (area == _drawn && view.Zoom == _drawnZoom && view.Pan == _drawnPan &&
                visible == _drawnVisible)
            {
                continue;
            }

            _drawn = area;
            _drawnZoom = view.Zoom;
            _drawnPan = view.Pan;
            _drawnVisible = visible;
            canvas.Clear(target);
            if (!visible)
                continue;

            CanvasGrid.Draw(
                canvas,
                target,
                area,
                new Vector2(AnimatorPreviewGrid.Cell, AnimatorPreviewGrid.Cell) * view.Zoom,
                AnimatorGeometry.PreviewOrigin(area, view));
        }
    }
}
