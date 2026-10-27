using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws the shared playfield for play scenes.</summary>
public sealed class PlayfieldDrawSystem :
    QuerySystem<PlayfieldView, Playfield, Playhead, PlayfieldMode>
{
    private readonly EntityStore _entities;
    private readonly IRectCanvas _canvas;
    private readonly Rid _playfield;

    public PlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Rid playfield)
    {
        _entities = entities;
        _canvas = canvas;
        _playfield = playfield;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_playfield);

        Query.ForEachEntity((
            ref view,
            ref playfield,
            ref playhead,
            ref mode,
            _) =>
        {
            if (mode.Kind == PlayfieldKind.Play)
            {
                PlayfieldRenderer.DrawPlay(
                    _canvas,
                    _playfield,
                    _entities,
                    view,
                    playfield,
                    playhead);
            }
        });
    }
}
