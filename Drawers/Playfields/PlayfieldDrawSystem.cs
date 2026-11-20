using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Playfields;

/// <summary>Draws the shared playfield for play scenes.</summary>
public sealed class PlayfieldDrawSystem(EntityStore entities, IRectCanvas canvas, Handle playfield) :
    QuerySystem<PlayfieldView, Playfield, Playhead, PlayfieldMode>
{
    /// <summary>A clock that keeps running through pauses, for anything drawn that moves on its own.</summary>
    private float _seconds;

    protected override void OnUpdate()
    {
        canvas.Clear(playfield);
        _seconds += Tick.deltaTime;
        Vector2 shift = Vector2.Zero;

        // One play entity at most.
        foreach (Entity entity in Query.Entities)
        {
            if (entity.GetComponent<PlayfieldMode>().Kind != PlayfieldKind.Play)
                continue;

            PlayfieldView view = entity.GetComponent<PlayfieldView>();
            Playfield field = entity.GetComponent<Playfield>();

            // The camera scrolls the whole layer, particles included; what stays
            // put on screen is drawn inside what it shows.
            Rect2 frame = PlayfieldMetrics.FieldRect(view, field);
            if (entity.TryGetComponent(out PlayCamera camera))
            {
                shift = camera.LayerShift;
                frame = frame.Intersection(camera.Visible);
            }

            PlayfieldRenderer.DrawPlay(canvas, playfield, entities, view, field, entity.GetComponent<Playhead>(), shift, frame, _seconds);
        }

        canvas.SetTransform(playfield, new Transform2D(0f, -shift));
    }
}
