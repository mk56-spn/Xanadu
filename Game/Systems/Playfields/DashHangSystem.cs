using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Systems.Physics;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The hang after a dash with no exit push, from <see cref="DashHangSystem"/>.
/// Times are playhead times, so the hang is counted from the dash note's
/// moment, not from the press.
/// </summary>
public struct DashHang(long startMs, float holdMs, float easeMs) : IComponent
{
    /// <summary>The playhead time the dash note fell on.</summary>
    public long StartMs = startMs;

    /// <summary>How long gravity stays off.</summary>
    public float HoldMs = holdMs;

    /// <summary>How long gravity then takes to come back in full.</summary>
    public float EaseMs = easeMs;
}

/// <summary>
/// Holds the player in the air after a dash instead of dropping it: gravity is
/// off for a quarter beat from the dash note, then eases back in over the
/// next quarter. The beat is read from the song's timing, so the hang fits the tempo.
/// </summary>
public sealed class DashHangSystem : QuerySystem<DashHang, PhysicsBody, Playhead>
{
    /// <summary>The shortest and longest the hang holds, so very fast or slow songs stay playable.</summary>
    public const float MinHoldMs = 50f;
    public const float MaxHoldMs = 175f;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.ForEachEntity((ref hang, ref body, ref playhead, entity) =>
        {
            float elapsed = playhead.TimeMs - hang.StartMs;
            float? scale = GravityScale(hang, elapsed);
            if (scale is null)
            {
                Box2DPhysicsWorld.SetGravityScale(body.Body, 1f);
                buffer.RemoveComponent<DashHang>(entity.Id);
                return;
            }

            Box2DPhysicsWorld.SetGravityScale(body.Body, scale.Value);
        });
    }

    /// <summary>
    /// How much gravity the hang lets through <paramref name="elapsed"/>
    /// milliseconds after the dash note: none while it holds, then easing to
    /// full. Null once the hang is over, or when the playhead has been taken
    /// far back before it, as by a restart.
    /// </summary>
    public static float? GravityScale(DashHang hang, float elapsed)
    {
        if (elapsed < -1_000f || elapsed >= hang.HoldMs + hang.EaseMs)
            return null;
        if (elapsed <= hang.HoldMs)
            return 0f;

        float t = (elapsed - hang.HoldMs) / Math.Max(hang.EaseMs, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>The hang for a dash note at <paramref name="noteTimeMs"/>: a quarter beat held, a quarter beat easing out.</summary>
    public static DashHang For(TempoMap tempo, long startMs, long noteTimeMs)
    {
        float quarter = (float)(TempoMap.BeatMs(tempo.At(noteTimeMs).Bpm) / 4);
        float hold = Math.Clamp(quarter, MinHoldMs, MaxHoldMs);
        return new DashHang(startMs, hold, hold);
    }

    /// <summary>Starts a hang, or restarts one already running.</summary>
    public static void Begin(Entity player, PhysicsBody body, CommandBuffer buffer, Playhead playhead, long startMs, long noteTimeMs)
    {
        Box2DPhysicsWorld.SetGravityScale(body.Body, 0f);
        buffer.AddComponent(player.Id, For(playhead.Tempo, startMs, noteTimeMs));
    }

    /// <summary>Ends any hang at once, as a momentum note or an exit push does.</summary>
    public static void End(Entity player, PhysicsBody body, CommandBuffer buffer)
    {
        Box2DPhysicsWorld.SetGravityScale(body.Body, 1f);
        if (player.HasComponent<DashHang>())
            buffer.RemoveComponent<DashHang>(player.Id);
    }
}
