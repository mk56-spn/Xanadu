using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Timeline;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>Updates the ECS editor header, footer, status, and preview labels.</summary>
public sealed class EditorHudSystem : QuerySystem<EditorHud, LevelInfo, Playhead>
{
    private readonly EntityStore _entities;
    private readonly Xanadu.Systems.Ui.ToastSystem? _toasts;
    private int _shownStatus;

    public EditorHudSystem(EntityStore entities, Xanadu.Systems.Ui.ToastSystem? toasts = null)
    {
        _entities = entities;
        _toasts = toasts;
    }

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;

        Query.ForEachEntity((ref hud, ref level, ref playhead, entity) =>
        {
            UiText.Set(hud.Level,
                string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
                buffer);
            UiText.Set(hud.Song, level.Song?.Title ?? "No song loaded", buffer);
            _shownStatus = ShellStatus.Show(_toasts, hud.StatusText, hud.StatusSerial, _shownStatus);
            UiText.Set(hud.Time,
                $"{TimeFormat.Clock(playhead.TimeMs)} / {TimeFormat.Clock(playhead.DurationMs)}",
                buffer);
            UiText.Set(hud.Notes,
                $"{_entities.Query<Note>().Count} notes",
                buffer);
        });
    }
}
