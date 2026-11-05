using System;
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
    private readonly IPassSurfaces? _passes;

    /// <summary>The clock grass sways by; it keeps running through pauses.</summary>
    private float _seconds;

    public PlayfieldDrawSystem(
        EntityStore entities,
        IRectCanvas canvas,
        Rid playfield,
        IPassSurfaces? passes = null,
        Func<Rect2, Rect2>? toPixels = null)
    {
        _entities = entities;
        _canvas = new ShaderRoutingCanvas(canvas, playfield, _shaderItems, toPixels);
        _playfield = playfield;
        _passes = passes;
    }

    protected override void OnUpdate()
    {
        _canvas.Clear(_playfield);
        _seconds += Tick.deltaTime;
        Vector2 shift = Vector2.Zero;

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
                _shaderItems.MaterialOf = (slot, blend) => LevelShaderMaterials.MaterialOf(shaders, slot, blend);
                _shaderItems.CommonMaterialOf = (path, blend) => CommonShaderMaterials.MaterialOf(path, blend);

                Color? background = null;
                Color? outside = null;
                Color? frameColor = null;
                if (entity.TryGetComponent(out LevelInfo info))
                {
                    background = info.Background;
                    outside = info.Outside;
                    frameColor = info.Frame;
                }

                // The camera scrolls the whole layer, characters and particles
                // included; what stays put on screen is drawn inside what it shows.
                Rect2 frame = PlayfieldRenderer.FieldRect(view, playfield);
                if (entity.TryGetComponent(out PlayCamera camera))
                {
                    shift = camera.LayerShift;
                    frame = frame.Intersection(camera.Visible);
                }

                PlayfieldRenderer.DrawPlay(
                    _canvas,
                    _playfield,
                    _entities,
                    view,
                    playfield,
                    playhead,
                    shaders.FieldSlot,
                    shaders.PlayerSlot,
                    background,
                    shift,
                    frame,
                    outside,
                    frameColor,
                    _seconds,
                    entity.TryGetComponent(out LevelGroups groups) ? groups.Groups : null,
                    entity.TryGetComponent(out LevelLayers layers) ? layers.Layers : null,
                    _passes);
            }
        });

        _canvas.SetTransform(_playfield, new Transform2D(0f, -shift));
    }
}
