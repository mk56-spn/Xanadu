using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// F10: draws the boxes play actually collides with — the player's box and
/// every block's box — over the playfield art, in both play and editor scenes.
/// The art covers the playfield canvas, so the boxes get their own canvas.
/// </summary>
public sealed class PlayfieldHitboxOverlaySystem : QuerySystem<PlayfieldView>
{
    private readonly EntityStore _entities;
    private readonly IRectCanvas _canvas;
    private readonly Rid _overlay;
    private readonly IOverlayToggleSource _toggle;

    private bool _visible;
    private bool _wasTogglePressed;

    public PlayfieldHitboxOverlaySystem(
        EntityStore entities,
        IRectCanvas canvas,
        Rid overlay,
        IOverlayToggleSource toggle)
    {
        _entities = entities;
        _canvas = canvas;
        _overlay = overlay;
        _toggle = toggle;
        _canvas.SetVisible(_overlay, false);
    }

    protected override void OnUpdate()
    {
        bool pressed = _toggle.IsTogglePressed();
        if (pressed && !_wasTogglePressed)
        {
            _visible = !_visible;
            _canvas.SetVisible(_overlay, _visible);
        }

        _wasTogglePressed = pressed;

        // Cleared even when hidden: the scene may have changed underneath it.
        _canvas.Clear(_overlay);
        if (!_visible)
            return;

        foreach (Entity entity in Query.ToEntityList())
        {
            PlayfieldRenderer.DrawHitboxes(
                _canvas,
                _overlay,
                _entities,
                entity.GetComponent<PlayfieldView>());
            return;
        }
    }
}
