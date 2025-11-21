using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Godot;
using MemoryPack;
using Xanadu.Singletons;
using XanaduProject.ECSComponents.Animation.Arrays;
using XanaduProject.ECSComponents.EntitySystem.Components;
using XanaduProject.GameDependencies;
using XanaduProject.Screens.AssetCreation.PoseAnimating;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;
using XanaduProject.Singleton;
using XanaduProject.Tools;

namespace XanaduProject.IO
{
    [MemoryPackable]
    public sealed partial record TrackDto(
        string Name,
        float[] Times,
        EasingType[]? Easings,      // null when no easing
        float[]? FloatValues,       // mutually exclusive
        Vector2[]? VectorValues     // mutually exclusive
    );

    [MemoryPackable]
    public sealed partial record AnimationDto(
        float Duration,
        List<TrackDto> Tracks
    );
    public static class AnimationIo
    {
        public static AnimationDto LoadAnimation(string path)
        {
            string filePath = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.ANIMATIONS_DIR), path);

            // MemoryPack deserialisation
            byte[] bytes = File.ReadAllBytes(filePath);
            return MemoryPackSerializer.Deserialize<AnimationDto>(bytes) ?? throw new InvalidOperationException();
        }

        public static void SaveAnimation(EntityStore store, string path)
        {
            string filePath = Path.Combine(ProjectSettings.GlobalizePath(SerializationUtils.ANIMATIONS_DIR), path);

            var dtoTracks = new List<TrackDto>();
            store.Query<FloatArrayEcs, NameEcs>().ForEachEntity(
                (ref FloatArrayEcs times, ref NameEcs name, Entity e) =>
                {
                    if (!e.HasComponent<AngleArrayEcs>() && !e.HasComponent<VectorArrayEcs>()) return;

                    dtoTracks.Add(new TrackDto(
                        name.Name,
                        times.Points,
                        e.TryGetComponent<EasingArrayEcs>(out var eas) ? eas.Points : null,
                        e.TryGetComponent<AngleArrayEcs> (out var ang) ? ang.Points : null,
                        e.TryGetComponent<VectorArrayEcs>(out var vec) ? vec.Points : null
                    ));
                });

            var dto = new AnimationDto(PoseAnimatingScreen.Info.Duration, dtoTracks);

            // MemoryPack serialisation
            byte[] bytes = MemoryPackSerializer.Serialize(dto);
            File.WriteAllBytes(filePath, bytes);

            Logger.AddLog(LogCategory.General, "Animation saved to: " + filePath);
        }
    }
}
