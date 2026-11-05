using System;
using System.IO;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Editor.Commands;
using Xanadu.Levels;
using Xanadu.Settings;

namespace Xanadu.Systems.Editor;

/// <summary>The editor's level and transport commands: metadata, playback, loading and saving.</summary>
public sealed class EditorLevelCommands(EditorCommandContext context)
{
    public bool TryApply(EditorCommand command, Entity editor, CommandBuffer buffer)
    {
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();

        switch (command.Type)
        {
            case EditorCommandType.SetName:
                level.Name = command.Text ?? "";
                return true;

            case EditorCommandType.SetArtist:
                level.Artist = command.Text ?? "";
                return true;

            case EditorCommandType.SetDifficulty:
                level.Difficulty = command.Text ?? "";
                return true;

            case EditorCommandType.SetBpm:
                playhead.Bpm = Math.Clamp(command.IntValue, 1, 1000);
                return true;

            case EditorCommandType.SetOffset:
                playhead.OffsetMs = Math.Clamp(command.LongValue, -600_000, 600_000);
                return true;

            case EditorCommandType.SetSnap:
                playhead.SnapDivision = NormalizeSnap(command.IntValue);
                return true;

            case EditorCommandType.SetPlacementMode:
                if (Enum.IsDefined(typeof(EditorPlacementMode), command.IntValue) &&
                    editor.HasComponent<EditorPlacement>())
                {
                    editor.GetComponent<EditorPlacement>().Mode = (EditorPlacementMode)command.IntValue;
                }

                return true;

            case EditorCommandType.TogglePlayback:
                if (!playhead.Playing &&
                    playhead.DurationMs > 0 &&
                    playhead.TimeMs >= playhead.DurationMs)
                {
                    playhead.SeekRequestMs = 0;
                }

                playhead.Playing = !playhead.Playing;
                return true;

            case EditorCommandType.StopPlayback:
                playhead.Playing = false;
                playhead.SeekRequestMs = 0;
                return true;

            case EditorCommandType.Seek:
                playhead.SeekRequestMs = Math.Max(0, command.LongValue);
                return true;

            case EditorCommandType.SetMetronome:
                editor.GetComponent<EditorMetronome>().Enabled = command.IntValue != 0;
                return true;

            case EditorCommandType.LoadSong:
                LoadSong(command.Text ?? "", editor);
                if (editor.HasComponent<TimelineView>())
                    editor.GetComponent<TimelineView>().AutoZoomed = false;
                return true;

            case EditorCommandType.LoadSongById:
                LoadSongById(command.Text ?? "", editor);
                if (editor.HasComponent<TimelineView>())
                    editor.GetComponent<TimelineView>().AutoZoomed = false;
                return true;

            case EditorCommandType.ApplyLevel:
                if (command.Document is not null)
                    ApplyLevel(command.Document, command.Text ?? "", editor, buffer);
                return true;

            case EditorCommandType.RecoverLevel:
                if (command.Document is not null)
                    ApplyLevel(command.Document, command.Text ?? "", editor, buffer, isOnDisk: false);
                return true;

            case EditorCommandType.SetBackgroundColor:
                level.Background = command.ColorValue;
                return true;

            case EditorCommandType.SetOutsideColor:
                level.Outside = command.ColorValue;
                return true;

            case EditorCommandType.SetFrameColor:
                level.Frame = command.ColorValue;
                return true;

            case EditorCommandType.SetLevelFile:
                SetLevelFile(editor, command.Text ?? "");
                // Directly after the query, so the autosave system later in
                // this group sees the save and clears its recovery file. The
                // shared command buffer would only apply it next frame, after
                // a preview may already have switched the scene.
                context.AfterQuery.Add(() => editor.AddTag<EditorSaved>());
                return true;

            default:
                return false;
        }
    }

    private static void LoadSongById(string id, Entity editor)
    {
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        var songs = new SongStore();
        SongDocument? song = songs.Find(id);
        if (song is null)
            return;

        level.SongId = song.Id;
        level.AudioPath = songs.ResolveAudioPath(song);
        level.Artist = song.Artist;
        playhead.Bpm = song.Bpm;
        playhead.OffsetMs = song.OffsetMs;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = -1;
        playhead.AudioRevision++;
    }

    private static void LoadSong(string path, Entity editor)
    {
        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        try
        {
            SongDocument song = new SongStore().Import(path);
            level.SongId = song.Id;
            level.AudioPath = song.AudioPath;
            level.Artist = song.Artist;
            playhead.Bpm = song.Bpm;
            playhead.OffsetMs = song.OffsetMs;
        }
        catch (Exception)
        {
            level.AudioPath = path;
        }

        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = -1;
        playhead.AudioRevision++;

        if (string.IsNullOrWhiteSpace(level.Name) || level.Name == "Untitled")
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(name))
                level.Name = name;
        }
    }

    private void ApplyLevel(
        LevelDocument document,
        string path,
        Entity editor,
        CommandBuffer buffer,
        bool isOnDisk = true)
    {
        document.Notes ??= new();
        document.Mines ??= new();
        document.StartPoints ??= new();
        document.Notes.RemoveAll(note => note is null);

        editor.GetComponent<EditorMetronome>().Enabled = false;
        context.History.Clear();
        LevelDocumentMapper.Apply(context.Entities, editor, document);

        if (editor.HasComponent<TimelineView>())
            editor.GetComponent<TimelineView>().AutoZoomed = false;

        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();

        SetLevelFile(editor, path);

        // A level straight from disk is the clean state to measure edits against.
        if (isOnDisk)
            buffer.AddTag<EditorBaselineStale>(editor.Id);
    }

    private static void SetLevelFile(Entity editor, string path)
    {
        if (editor.HasComponent<LevelFile>())
            editor.GetComponent<LevelFile>().Path = path;
    }

    private static int NormalizeSnap(int snap) => snap switch
    {
        8 => 8,
        16 => 16,
        _ => 4,
    };
}
