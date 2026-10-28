using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>Updates the ECS editor header, footer, status, and preview labels.</summary>
public sealed class EditorHudSystem : QuerySystem<EditorHud, LevelInfo, Playhead>
{
    private readonly EntityStore _entities;

    public EditorHudSystem(EntityStore entities)
    {
        _entities = entities;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref hud, ref level, ref playhead, entity) =>
        {
            UiText.Set(hud.Level,
                string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
                buffer);
            UiText.Set(hud.Song,
                "Song: " + (string.IsNullOrWhiteSpace(level.AudioPath)
                    ? "No song loaded"
                    : Path.GetFileName(level.AudioPath)),
                buffer);
            UiText.Set(hud.Status, hud.StatusText, buffer);
            UiText.Set(hud.Time,
                $"{FormatMs(playhead.TimeMs)} / {FormatMs(playhead.DurationMs)}",
                buffer);
            UiText.Set(hud.Notes,
                $"{_entities.Query<Note>().Count} notes",
                buffer);
        });
    }

    private static string FormatMs(long milliseconds)
    {
        long value = Math.Max(0, milliseconds);
        return $"{value / 60000:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }
}
