using System;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Model.Levels;
using Xanadu.Storage.Levels;

namespace Xanadu.Logic.Editor;

/// <summary>The editor's level and transport commands: metadata, playback, loading and saving.</summary>
public sealed class EditorLevelCommands(EditorCommandContext context)
{
    public void Listen(Entity editor)
    {
        context.On<SetLevelName>(editor, (on, intent) => on.GetComponent<LevelInfo>().Name = intent.Name);
        context.On<SetLevelArtist>(editor, (on, intent) => on.GetComponent<LevelInfo>().Artist = intent.Artist);
        context.On<SetLevelDifficulty>(editor, (on, intent) => on.GetComponent<LevelInfo>().Difficulty = intent.Difficulty);
        context.On<SetSnap>(editor, (on, intent) => on.GetComponent<Playhead>().SnapDivision = NormalizeSnap(intent.Division));
        context.On<PickPlacement>(editor, (on, intent) =>
        {
            if (Enum.IsDefined(intent.Mode) && on.HasComponent<EditorPlacement>())
                on.GetComponent<EditorPlacement>().Mode = intent.Mode;
        });
        context.On<TogglePlayback>(editor, (on, _) =>
        {
            ref Playhead playhead = ref on.GetComponent<Playhead>();
            if (!playhead.Playing && playhead.DurationMs > 0 && playhead.TimeMs >= playhead.DurationMs)
                playhead.SeekRequestMs = 0;

            playhead.Playing = !playhead.Playing;
        });
        context.On<StopPlayback>(editor, (on, _) =>
        {
            ref Playhead playhead = ref on.GetComponent<Playhead>();
            playhead.Playing = false;
            playhead.SeekRequestMs = 0;
        });
        context.On<Seek>(editor, (on, intent) => on.GetComponent<Playhead>().SeekRequestMs = Math.Max(0, intent.TimeMs));
        context.On<SetMetronome>(editor, (on, intent) => on.GetComponent<EditorMetronome>().Enabled = intent.On);
        context.On<UseSong>(editor, (on, intent) =>
        {
            if (SongStore.Shared.Open(intent.Id) is { } song)
                UseLibrarySong(song, on);
            if (on.HasComponent<TimelineView>())
                on.GetComponent<TimelineView>().AutoZoomed = false;
        });
        context.On<RecoverLevel>(editor, (on, intent) =>
        {
            if (intent.Level is not null)
                Recover(intent.Level, intent.Path ?? "", on);
        });
        context.On<SetLevelFile>(editor, (on, intent) =>
        {
            SetLevelFile(on, intent.Path ?? "");
            // Directly after the commands, so the autosave system later in
            // this group sees the save and clears its recovery file. The
            // shared command buffer would only apply it next frame, after
            // a preview may already have switched the scene.
            context.AfterQuery.Add(() => on.AddTag<EditorSaved>());
        });
    }

    private static void UseLibrarySong(LoadedSong song, Entity editor)
    {
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        level.SongId = song.Id;
        level.Song = song;
        level.Artist = song.Artist;
        playhead.Tempo = song.Tempo();
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = -1;
        playhead.AudioRevision++;
    }

    /// <summary>Puts unsaved work back: it is not what is on disk, so the baseline stays.</summary>
    private void Recover(LevelDocument document, string path, Entity editor)
    {
        document.Notes ??= [];
        document.Notes.RemoveAll(note => note is null);

        editor.GetComponent<EditorMetronome>().Enabled = false;
        context.History.Clear();
        LevelDocumentMapper.Apply(context.Entities, editor, document, SongStore.Shared.Open(document.SongId));

        if (editor.HasComponent<TimelineView>())
            editor.GetComponent<TimelineView>().AutoZoomed = false;

        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();

        SetLevelFile(editor, path);
    }

    private static void SetLevelFile(Entity editor, string path)
    {
        if (editor.HasComponent<FilePath>())
            editor.GetComponent<FilePath>().Value = path;
    }

    /// <summary>A beat snap divisor the editor offers; anything else falls back to 1/4.</summary>
    private static int NormalizeSnap(int snap) =>
        Array.Exists(SnapDivisions.Offered, offered => offered.Division == snap) ? snap : 4;
}
