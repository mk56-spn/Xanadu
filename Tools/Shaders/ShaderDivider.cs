using Friflo.Engine.ECS;
using Xanadu.Host.Tools;
using Xanadu.Host.Tools.ShaderEditing;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// The bar between the code and the preview column: a press on it drags the column's edge. Only the column's
/// container width changes (and the preview's height with it, kept 16:9); the code takes what is left.
/// </summary>
internal sealed class ShaderDivider(Entity body, Entity side, Entity preview, Entity divider, float gap)
{
    /// <summary>The preview column's width at first, and the least the code and the column can be dragged to.</summary>
    public const float SideWidth = 680, MinCode = 360;

    private const float MinSide = 280;

    /// <summary>Room the column keeps under the preview for problems, sliders and help.</summary>
    private const float MinUnderPreview = 260;

    private float _width = SideWidth;
    private float _grabbedAt;
    private bool _wasHeld;

    /// <summary>Whether the edge is being dragged, so the code takes no pointer.</summary>
    public bool Dragging { get; private set; }

    /// <summary>The preview's height for a column <paramref name="width"/> wide: 16:9.</summary>
    public static float PreviewHeight(float width) => MathF.Round(width * 9f / 16f);

    public void Update(in ToolFrame frame, ShaderCodeEditor code)
    {
        bool pressed = frame.Held && !_wasHeld;
        _wasHeld = frame.Held;
        Rect2 bar = UiLayout.Box(divider);
        if (pressed && bar.HasPoint(frame.Mouse))
        {
            Dragging = true;
            _grabbedAt = frame.Mouse.X - bar.GetCenter().X;
        }

        if (Dragging)
        {
            float width = Clamp(UiLayout.Box(side).End.X - (frame.Mouse.X - _grabbedAt) - gap / 2);
            if (width != _width)
            {
                _width = width;
                UiNode.Wrap(side).Width(width);
                UiNode.Wrap(preview).Height(PreviewHeight(width));
            }

            if (!frame.Held)
            {
                Dragging = false;
                // Pressing outside the code took its focus; dragging shouldn't cost the typing.
                code.Focus();
            }
        }

        code.DividerLit = Dragging || bar.HasPoint(frame.Mouse);
    }

    /// <summary>A column width that leaves the code its least, and room under the preview.</summary>
    private float Clamp(float width)
    {
        Rect2 whole = UiLayout.Box(body);
        float most = MathF.Min(whole.Size.X - gap - MinCode, (whole.Size.Y - gap - MinUnderPreview) * 16f / 9f);
        return MathF.Round(Math.Clamp(width, MinSide, MathF.Max(MinSide, most)));
    }
}
