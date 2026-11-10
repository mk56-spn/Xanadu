using System;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Systems.Input;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>The preview's wheel zoom and middle-drag pan, which own no selection.</summary>
public sealed class AnimatorPreviewNav(IWheelSource? wheel = null)
{
    private bool _middleHeld;
    private Vector2 _lastMiddle;

    public void Zoom(Entity session, ref AnimatorPreviewView view, Rect2 area, PointerInput input)
    {
        if (wheel is null || !area.HasPoint(input.Position))
            return;

        int notches = wheel.TakeWheel();
        if (notches == 0)
            return;

        view.ZoomAt(
            input.Position,
            area,
            Mathf.Pow(AnimatorPreviewView.WheelStep, notches));
        AnimatorSession.SetStatus(session, $"ZOOM x{view.Zoom:0.##}");
    }

    /// <summary>Middle-drag panning, independent of selection; true while it owns the pointer.</summary>
    public bool Pan(ref AnimatorPreviewView view, Rect2 area, PointerInput input)
    {
        if (!input.Middle)
        {
            _middleHeld = false;
            return false;
        }

        if (!_middleHeld)
        {
            _middleHeld = true;
            _lastMiddle = input.Position;
            return true;
        }

        if (area.HasPoint(input.Position))
            view.Pan += input.Position - _lastMiddle;
        _lastMiddle = input.Position;
        return true;
    }
}
