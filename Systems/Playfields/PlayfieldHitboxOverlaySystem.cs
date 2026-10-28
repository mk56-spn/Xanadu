using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Input;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// F10: draws what play actually collides and input-checks with — the player's
/// box, every block's box, and the sphere notes are checked against — over the
/// playfield art, in both play and editor scenes. The art covers the playfield
/// canvas, so the boxes get their own canvas.
/// </summary>
public sealed class PlayfieldHitboxOverlaySystem : QuerySystem<PlayfieldView>
{
    private readonly EntityStore _entities;
    private readonly IRectCanvas _canvas;
    private readonly Rid _overlay;
    private readonly OverlayLatch _latch;

    public PlayfieldHitboxOverlaySystem(
        EntityStore entities,
        IRectCanvas canvas,
        Rid overlay,
        IOverlayToggleSource toggle)
    {
        _entities = entities;
        _canvas = canvas;
        _overlay = overlay;
        _latch = new OverlayLatch(toggle);
        _canvas.SetVisible(_overlay, false);
    }

    protected override void OnUpdate()
    {
        if (_latch.Update())
            _canvas.SetVisible(_overlay, _latch.Visible);

        // Cleared even when hidden: the scene may have changed underneath it.
        _canvas.Clear(_overlay);
        if (!_latch.Visible)
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
