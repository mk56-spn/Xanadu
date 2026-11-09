using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Tools.Animator.Commands;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Routes preview pointer input: zoom and pan, hover, then the press to a grab
/// and the following frames to a drag. What each of those does lives in
/// <see cref="AnimatorPreviewPick"/>, <see cref="AnimatorPreviewGrab"/>,
/// <see cref="AnimatorPreviewDrag"/>, <see cref="AnimatorPreviewIk"/> and
/// <see cref="AnimatorPreviewNav"/>.
/// </summary>
public sealed class AnimatorPreviewInputSystem(
    IPointerInputSource pointer,
    IWheelSource? wheel = null) : QuerySystem<AnimatorDocument, AnimatorUi>
{
    private readonly AnimatorPreviewNav _nav = new(wheel);
    private bool _panelHeld;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
            Update(session);
    }

    private void Update(Entity session)
    {
        if (session.GetComponent<AnimatorNewClipDialog>().Open)
        {
            session.GetComponent<AnimatorHover>() = AnimatorHover.None;
            return;
        }

        Entity panel = session.GetComponent<AnimatorUi>().Preview;
        PointerInput input = pointer.Read();
        Rect2 area = AnimatorGeometry.PanelArea(panel);
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();

        _nav.Zoom(session, ref view, area, input);

        // Preview mode still pans and zooms, but picks and drags nothing.
        if (_nav.Pan(ref view, area, input) || AnimatorPreviewModeCommands.IsOn(session))
        {
            session.GetComponent<AnimatorHover>() = AnimatorHover.None;
            return;
        }

        bool pressed = !panel.IsNull && panel.Tags.Has<Pressed>();
        bool begin = pressed && !_panelHeld;
        _panelHeld = pressed;

        AnimatorPreviewPick.Hover(session, area, input, !input.Pressed && !begin);
        if (session.GetComponent<AnimatorWeightPaint>().Active && area.HasPoint(input.Position))
            session.GetComponent<AnimatorWeightPaint>().Pointer = input.Position;

        ref AnimatorDrag drag = ref session.GetComponent<AnimatorDrag>();
        if (begin)
        {
            AnimatorPreviewGrab.Begin(session, input.Position, area, input.Shift, input.Ctrl);
        }
        else if (drag.Kind == AnimatorDragKind.PaintWeights)
        {
            if (input.Pressed)
                AnimatorWeightCommands.Stroke(session, input.Position,
                    AnimatorGeometry.PreviewPlacement(area, view), input.Ctrl);
            else
                AnimatorPreviewDrag.End(session);
        }
        else if (drag.Kind is AnimatorDragKind.Bone or AnimatorDragKind.Attachment
                     or AnimatorDragKind.Swing or AnimatorDragKind.Ik or AnimatorDragKind.Rotate ||
                 AnimatorChainHandles.IsChainDrag(drag.Kind) || AnimatorTargetCommands.IsDrag(drag.Kind))
        {
            if (input.Pressed)
            {
                AnimatorPreviewGrab.FollowModes(session, input.Position, area);
                AnimatorPreviewDrag.Drag(session, input.Position, area, input.Shift);
            }
            else
                AnimatorPreviewDrag.End(session);
        }
    }
}
