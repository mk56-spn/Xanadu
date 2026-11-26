using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animator;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Animator;

// Reads the pointer outside UI (BannedSymbols.txt): viewport: bone drags in the preview, until the viewport has its own design.
#pragma warning disable RS0030

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
        Entity xif = session.GetComponent<AnimatorUi>().XifEditor;
        if (AnimatorUi.Of(session).NewClipDialog.IsOpen() || Xanadu.Ui.Systems.Ui.XifEditorSystem.IsOpen(xif))
        {
            session.GetComponent<AnimatorHover>() = AnimatorHover.None;

            // A part edited on its bone: the preview under the see-through editor still zooms and pans.
            if (!AnimatorUi.Of(session).NewClipDialog.IsOpen() &&
                xif.GetComponent<Xanadu.Ui.Ecs.Ui.XifEditorFrame>().Placement is not null)
            {
                PointerInput over = pointer.Read();
                Rect2 under = AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().Preview);
                ref AnimatorPreviewView moved = ref session.GetComponent<AnimatorPreviewView>();
                _nav.Zoom(session, ref moved, under, over);
                _nav.Pan(ref moved, under, over);
            }
            return;
        }

        Entity panel = session.GetComponent<AnimatorUi>().Preview;
        PointerInput input = pointer.Read();
        Rect2 area = AnimatorGeometry.PanelArea(panel);
        ref AnimatorPreviewView view = ref session.GetComponent<AnimatorPreviewView>();

        _nav.Zoom(session, ref view, area, input);

        // Preview mode still pans and zooms, but picks and drags nothing.
        if (_nav.Pan(ref view, area, input) || AnimatorPreviewMode.IsOn(session))
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
