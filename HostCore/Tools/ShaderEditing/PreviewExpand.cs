using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Theme;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems;
using Xanadu.Ui.Systems.Canvas;
using Xanadu.Ui.Systems.Input;

namespace Xanadu.Host.Tools.ShaderEditing;

/// <summary>
/// The preview's expand button, media-player style: it sits in the preview's top-right corner and grows the preview
/// over the whole stage, above the editor; pressed again (or Escape), the preview goes back to its slot. The button
/// rides on a frame that follows the preview's box, on a layer of its own over the preview.
/// </summary>
internal sealed class PreviewExpand
{
    private const string Icon = "Assets/Icons/fit.svg";

    private readonly UiNode _layer, _frame, _button;
    private readonly Handle _item;
    private Rect2 _framed;
    private bool _held, _escapeHeld;

    /// <summary>Whether a preview starts expanded (the shader tool's <c>--expand</c>).</summary>
    public static bool StartExpanded { get; set; }

    /// <summary>Whether the preview fills the stage.</summary>
    public bool Expanded { get; private set; }

    /// <param name="item">The preview's canvas item: lifted over the editor while expanded.</param>
    public PreviewExpand(EntityStore entities, Handle item)
    {
        _item = item;
        _layer = UiNode.Layer(entities, default, UiLayers.ShaderPreviewControls, null, UiStage.Full).Theme(UiThemes.Tools);
        _frame = _layer.Column().IgnoreLayout();
        _button = _frame.IconButton("[ ]", Icon, UiMetrics.Control.Sm).Secondary().Anchor(UiEdge.TopRight, UiMetrics.Space.Sm)
            .Tooltip("Fill the window with the shader; press again or Escape for the editor");
        if (StartExpanded)
            Flip();
    }

    /// <summary>
    /// Each frame: a press of the button (or Escape while expanded) flips it, and the button follows the preview to
    /// <paramref name="slot"/> or the whole stage. Returns where the preview draws when expanded, null in its slot.
    /// </summary>
    public Rect2? Follow(Rect2 slot)
    {
        bool pressed = _button.Entity.Tags.Has<Pressed>();
        bool escape = HostPlatform.Keyboard.IsPressed(Key.Escape);
        if ((pressed && !_held) || (escape && !_escapeHeld && Expanded))
            Flip();
        (_held, _escapeHeld) = (pressed, escape);

        Rect2 shown = Expanded ? UiStage.Full.Bounds : slot;
        if (shown != _framed && shown.Size.X > 0)
        {
            _framed = shown;
            _frame.Box(shown);
        }

        return Expanded ? UiStage.Full.Bounds : null;
    }

    public void Delete() => UiLayout.DeleteSubtree(_layer.Entity);

    private void Flip()
    {
        Expanded = !Expanded;
        // Bright while on, muted at rest.
        if (Expanded)
            _button.Primary();
        else
            _button.Secondary();
        _button.Entity.AddTag<Dirty>();
        CanvasServer.Current.SetZIndex(_item, Expanded ? UiLayers.ShaderPreviewFull : 0);
    }
}
