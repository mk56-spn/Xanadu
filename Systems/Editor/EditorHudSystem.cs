using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Updates the ECS editor header, footer, status, and preview labels.</summary>
public sealed class EditorHudSystem : QuerySystem<EditorHud, LevelInfo, Playhead>
{
    private static readonly Vector2 Hidden = new(-10_000, -10_000);
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
            SetText(hud.Level,
                string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
                buffer);
            SetText(hud.Song,
                "Song: " + (string.IsNullOrWhiteSpace(level.AudioPath)
                    ? "No song loaded"
                    : Path.GetFileName(level.AudioPath)),
                buffer);
            SetText(hud.Status, hud.StatusText, buffer);
            SetText(hud.Time,
                $"{FormatMs(playhead.TimeMs)} / {FormatMs(playhead.DurationMs)}",
                buffer);
            SetText(hud.Notes,
                $"{_entities.Query<Note>().Count} notes",
                buffer);

            bool preview = entity.HasComponent<EditorPreview>() &&
                           entity.GetComponent<EditorPreview>().Active;
            SetText(hud.Preview,
                preview
                    ? $"PREVIEW - {level.Name}   " +
                      $"{playhead.TimeMs / 1000f:0.00}s / " +
                      $"{playhead.DurationMs / 1000f:0.00}s   Esc to return"
                    : "",
                buffer);

            if (!hud.Preview.IsNull && hud.Preview.HasComponent<Position>())
            {
                ref Position position = ref hud.Preview.GetComponent<Position>();
                Vector2 target = preview
                    ? hud.PreviewHome
                    : Hidden;
                if (position.Value != target)
                {
                    position.Value = target;
                    buffer.AddTag<Moved>(hud.Preview.Id);
                }
            }
        });
    }

    private static void SetText(Entity entity, string value, CommandBuffer buffer)
    {
        if (entity.IsNull || !entity.HasComponent<Text>())
            return;

        ref Text text = ref entity.GetComponent<Text>();
        if (text.Value == value)
            return;

        text.Value = value;
        buffer.AddTag<Dirty>(entity.Id);
    }

    private static string FormatMs(long milliseconds)
    {
        long value = Math.Max(0, milliseconds);
        return $"{value / 60000:00}:{value / 1000 % 60:00}.{value % 1000:000}";
    }
}
