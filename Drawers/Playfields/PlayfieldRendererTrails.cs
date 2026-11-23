using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Drawers.Playfields;

public static partial class PlayfieldRenderer
{
    /// <summary>
    /// The player's beam (<see cref="Beams.Look"/>: a bright line into the judgement
    /// line that thins and fades, with a flash across its end where it strikes),
    /// drawn by the Xif drawer stretched from where it starts to where it strikes.
    /// </summary>
    public static void DrawHitTrail(IRectCanvas canvas, Handle rid, Entity entity)
    {
        HitTrail trail = entity.GetComponent<HitTrail>();
        Vector2 way = trail.Position - trail.From;
        float length = way.Length();
        if (length < 0.5f)
            return;

        Vector2 along = way / length;
        var across = new Vector2(-along.Y, along.X);
        Beams.SetState(length, Math.Clamp(trail.Age / Math.Max(0.01f, trail.Lifetime), 0f, 1f), trail.Color);
        var placement = new Transform2D(along * (length + Beams.Past), across * Beams.HalfWidth, trail.From);
        XifDrawer.Shared.DrawNode(canvas, rid, Beams.Look, placement, 1f, XifTint.Plain);
    }
}
