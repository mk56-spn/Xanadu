using System;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Systems.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Editor;

/// <summary>Updates the ECS editor header, footer, status, and preview labels.</summary>
public sealed class EditorHudSystem : QuerySystem<EditorHud, LevelInfo, Playhead>
{
    private readonly EntityStore _entities;
    private readonly ToastSystem? _toasts;
    private int _shownStatus;

    public EditorHudSystem(EntityStore entities, ToastSystem? toasts = null)
    {
        _entities = entities;
        _toasts = toasts;
    }

    protected override void OnUpdate()
    {
        Query.Each(new ShowHud(this, CommandBuffer));
    }

    private readonly record struct ShowHud(EditorHudSystem System, CommandBuffer Buffer) : IEach<EditorHud, LevelInfo, Playhead>
    {
        public void Execute(ref EditorHud hud, ref LevelInfo level, ref Playhead playhead)
        {
            UiText.Set(hud.Level,
                string.IsNullOrWhiteSpace(level.Name) ? "Untitled" : level.Name,
                Buffer);
            UiText.Set(hud.Song, level.Song?.Title ?? "No song loaded", Buffer);
            System._shownStatus = ShellStatus.Show(System._toasts, hud.StatusText, hud.StatusSerial, System._shownStatus);
            UiText.Set(hud.Time,
                $"{TimeFormat.Clock((long)playhead.ShownMs)} / {TimeFormat.Clock(playhead.DurationMs)}",
                Buffer);
            UiText.Set(hud.Notes,
                $"{System._entities.Query<Note>().Count} notes",
                Buffer);
        }
    }
}
