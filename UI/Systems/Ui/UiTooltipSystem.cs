using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Input;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Ui;

/// <summary>
/// Shows the <see cref="UiTooltip"/> of the topmost hovered control after a
/// short delay. The panel follows the pointer, flips above it near the bottom,
/// and stays inside the screen. Disabled controls never get a hover tag.
/// </summary>
public sealed class UiTooltipSystem : QuerySystem<UiTooltip>
{
    private const float Height = 26f;
    private const float OffsetX = 14f;
    private const float OffsetY = 20f;
    private const float HoverDelay = 0.35f;

    private readonly EntityStore _entities;
    private readonly IPointerInputSource _pointer;
    private readonly Vector2 _screen;
    private readonly Entity _tooltip;

    private int _hoveredId;
    private float _hoverSeconds;

    public UiTooltipSystem(
        EntityStore entities,
        IPointerInputSource pointer,
        IVisualRidFactory visuals,
        Vector2 screen)
    {
        _entities = entities;
        _pointer = pointer;
        _screen = screen;

        UiBuilder ui = UiBuilder.Begin(entities, visuals).Layer(UiLayers.Tooltip);
        _tooltip = ui.RectEntity(
            Vector2.Zero,
            UiTooltipPanel.Size("", Height),
            new Color(UiTheme.Tooltip, 0.96f),
            "",
            takesInput: false);
        _tooltip.AddComponent(new TextStyle(UiTheme.SmallFontSize, UiTheme.Text));
        UiLayout.SetHidden(_tooltip, true);
    }

    /// <summary>The tooltip panel entity, exposed for tests.</summary>
    public Entity Tooltip => _tooltip;

    protected override void OnUpdate()
    {
        if (_tooltip.IsNull)
            return;

        PointerInput input = _pointer.Read();
        Entity hovered = UiTooltipPanel.Hovered(_entities);

        if (hovered.IsNull ||
            hovered.Tags.Has<UiDisabled>() ||
            input.Pressed ||
            input.SecondaryPressed ||
            input.Middle ||
            string.IsNullOrEmpty(hovered.GetComponent<UiTooltip>().Text))
        {
            Hide();
            _hoveredId = 0;
            _hoverSeconds = 0f;
            return;
        }

        if (hovered.Id != _hoveredId)
        {
            _hoveredId = hovered.Id;
            _hoverSeconds = 0f;
            Hide();
        }

        _hoverSeconds += Tick.deltaTime;
        if (_hoverSeconds < HoverDelay)
        {
            Hide();
            return;
        }

        Show(hovered.GetComponent<UiTooltip>().Text, input.Position);
    }

    private void Show(string text, Vector2 pointer)
    {
        Vector2 size = UiTooltipPanel.Size(text, Height);
        Vector2 topLeft = pointer + new Vector2(OffsetX, OffsetY);

        if (_screen != Vector2.Zero)
        {
            if (topLeft.X + size.X > _screen.X - 6f)
                topLeft.X = pointer.X - size.X - OffsetX;
            if (topLeft.Y + size.Y > _screen.Y - 6f)
                topLeft.Y = pointer.Y - size.Y - 10f;

            topLeft.X = Mathf.Clamp(topLeft.X, 6f, Math.Max(6f, _screen.X - size.X - 6f));
            topLeft.Y = Mathf.Clamp(topLeft.Y, 6f, Math.Max(6f, _screen.Y - size.Y - 6f));
        }

        UiTooltipPanel.SetContent(_tooltip, text, size);
        UiLayout.SetHidden(_tooltip, false);
        ref Position position = ref _tooltip.GetComponent<Position>();
        Vector2 center = topLeft + size / 2f;
        if (position.Value != center)
        {
            position.Value = center;
            _tooltip.AddTag<Moved>();
        }
    }

    private void Hide()
    {
        if (_tooltip.IsNull)
            return;

        UiTooltipPanel.Clear(_tooltip);
        UiLayout.SetHidden(_tooltip, true);
    }
}
