using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;

namespace Xanadu.Scenes;

/// <summary>Creates a single miniaudio-backed track for the timeline test.</summary>
public static class TrackScene
{
    public static void Build(EntityStore entities, Vector2 viewportSize) =>
        Load(entities, "res://Audio/test-track.wav", durationMs: 20_000, bpm: 128f);

    public static void Load(
        EntityStore entities,
        string audioPath,
        long durationMs = 60_000,
        float bpm = 120f)
    {
        foreach (Entity entity in entities.Query<TrackProgress>().ToEntityList())
            entity.DeleteEntity();

        entities.CreateEntity(new TrackProgress(audioPath, durationMs, bpm));
    }
}
