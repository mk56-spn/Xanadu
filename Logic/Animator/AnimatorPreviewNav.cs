using System;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Animator;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Logic.Animator;

// Reads the pointer outside UI (BannedSymbols.txt): viewport: preview pan and zoom, until the viewport has its own design.
#pragma warning disable RS0030

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
            MathF.Pow(AnimatorPreviewView.WheelStep, notches));
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
