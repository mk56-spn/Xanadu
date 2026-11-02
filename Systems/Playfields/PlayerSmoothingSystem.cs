using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Works out where each player is drawn this frame. Between physics steps it
/// eases along the last step; anything else that moved the player since, such
/// as a dash, a wrap or the arena's floor, is a jump to draw straight away.
/// </summary>
public sealed class PlayerSmoothingSystem : QuerySystem<PlayerSmoothing, Position>
{
    /// <summary>Pixels the position may differ from the step's before it counts as a jump.</summary>
    private const float JumpSlack = 0.01f;

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref smoothing, ref position, _) =>
        {
            if (position.Value.DistanceTo(smoothing.Stepped) > JumpSlack)
            {
                smoothing.Previous = position.Value;
                smoothing.Stepped = position.Value;
            }

            smoothing.Drawn = smoothing.Previous.Lerp(smoothing.Stepped, smoothing.Alpha);
        });
    }
}
