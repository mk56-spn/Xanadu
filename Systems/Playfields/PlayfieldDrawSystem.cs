using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Playfields;

/// <summary>Draws the shared playfield for play scenes.</summary>
public sealed class PlayfieldDrawSystem :
    QuerySystem<PlayfieldView, Playfield, Playhead, PlayfieldMode>
{
    private readonly EntityStore _entities;
    private readonly ShaderRoutingCanvas _canvas;
    private readonly GodotShaderCanvasItems _shaderItems = new();
    private readonly Rid _playfield;

    public PlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Rid playfield)
    {
        _entities = entities;
        _canvas = new ShaderRoutingCanvas(canvas, playfield, _shaderItems);
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
            entity) =>
        {
            if (mode.Kind == PlayfieldKind.Play)
            {
                LevelShaders shaders = entity.TryGetComponent(out LevelShaders found)
                    ? found
                    : LevelShaders.Create();
                LevelShaderMaterials.Ensure(shaders);
                _shaderItems.MaterialOf = slot => LevelShaderMaterials.MaterialOf(shaders, slot);

                PlayfieldRenderer.DrawPlay(
                    _canvas,
                    _playfield,
                    _entities,
                    view,
                    playfield,
                    playhead,
                    shaders.FieldSlot,
                    shaders.PlayerSlot);
            }
        });
    }
}
