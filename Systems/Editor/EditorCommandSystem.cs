using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Levels;
using Writer.Settings;
using Writer.Editor;
using Writer.Editor.Commands;

namespace Writer.Systems.Editor;

/// <summary>Single ECS writer for editor intents from the Godot UI.</summary>
public sealed class EditorCommandSystem :
    QuerySystem<LevelInfo, Playfield, Playhead, EditorPreview, EditorMetronome>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly EditorHistory _history;
    private readonly List<ClipboardItem> _clipboard = new();

    public EditorCommandSystem(
        EntityStore entities,
        EditorCommandBus commands,
        EditorHistory history)
    {
        _entities = entities;
        _commands = commands;
        _history = history;
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((
            ref level,
            ref playfield,
            ref playhead,
            ref preview,
            ref metronome,
            entity) =>
        {
            while (_commands.TryDequeue(out EditorCommand command))
            {
                Apply(
                    command,
                    ref level,
                    ref playfield,
                    ref playhead,
                    ref preview,
                    ref metronome,
                    entity);
            }
        });
    }

    private void Apply(
        EditorCommand command,
        ref LevelInfo level,
        ref Playfield playfield,
        ref Playhead playhead,
        ref EditorPreview preview,
        ref EditorMetronome metronome,
        Entity editor)
    {
        switch (command.Type)
        {
            case EditorCommandType.SetName:
                level.Name = command.Text ?? "";
                break;

            case EditorCommandType.SetArtist:
                level.Artist = command.Text ?? "";
                break;

            case EditorCommandType.SetDifficulty:
                level.Difficulty = command.Text ?? "";
                break;

            case EditorCommandType.SetBpm:
                playhead.Bpm = Math.Clamp(command.IntValue, 1, 1000);
                break;

            case EditorCommandType.SetOffset:
                playhead.OffsetMs = Math.Clamp(command.LongValue, -600_000, 600_000);
                break;

            case EditorCommandType.SetSnap:
                playhead.SnapDivision = NormalizeSnap(command.IntValue);
                break;

            case EditorCommandType.TogglePlayback:
                if (!playhead.Playing &&
                    playhead.DurationMs > 0 &&
                    playhead.TimeMs >= playhead.DurationMs)
                {
                    playhead.SeekRequestMs = 0;
                }

                playhead.Playing = !playhead.Playing;
                break;

            case EditorCommandType.StopPlayback:
                playhead.Playing = false;
                playhead.SeekRequestMs = 0;
                break;

            case EditorCommandType.Seek:
                playhead.SeekRequestMs = Math.Max(0, command.LongValue);
                break;

            case EditorCommandType.SetMetronome:
                metronome.Enabled = command.IntValue != 0;
                break;

            case EditorCommandType.BeginPreview:
                BeginPreview(ref playhead, ref preview);
                break;

            case EditorCommandType.EndPreview:
                EndPreview(ref playhead, ref preview);
                break;

            case EditorCommandType.LoadSong:
                LoadSong(command.Text ?? "", ref level, ref playhead);
                if (editor.HasComponent<EditorTimeline>())
                    editor.GetComponent<EditorTimeline>().AutoZoomed = false;
                break;

            case EditorCommandType.LoadSongById:
                LoadSongById(command.Text ?? "", ref level, ref playhead);
                if (editor.HasComponent<EditorTimeline>())
                    editor.GetComponent<EditorTimeline>().AutoZoomed = false;
                break;

            case EditorCommandType.DeleteSelection:
                DeleteSelection(editor);
                break;

            case EditorCommandType.CopySelection:
                CopySelection(editor);
                break;

            case EditorCommandType.PasteSelection:
                PasteSelection(editor, ref playhead);
                break;

            case EditorCommandType.SetTarget:
                SetTarget(command.IntValue, editor);
                break;

            case EditorCommandType.MoveSelection:
                MoveSelection(command.IntValue, editor);
                break;

            case EditorCommandType.ZoomTimeline:
                ZoomTimeline(command.IntValue, editor);
                break;

            case EditorCommandType.Undo:
                if (_history.Undo())
                    ClearSelection(editor);
                break;

            case EditorCommandType.Redo:
                if (_history.Redo())
                    ClearSelection(editor);
                break;

            case EditorCommandType.ApplyLevel:
                if (command.Document is null)
                    break;

                ApplyLevel(
                    command.Document,
                    command.Text ?? "",
                    editor,
                    ref preview,
                    ref metronome);
                break;

            case EditorCommandType.SetLevelFile:
                SetLevelFile(editor, command.Text ?? "");
                break;
        }
    }

    private static void BeginPreview(ref Playhead playhead, ref EditorPreview preview)
    {
        if (preview.Active)
            return;

        long start = playhead.TimeMs;
        if (playhead.DurationMs > 0 && start >= playhead.DurationMs)
            start = 0;

        preview.Active = true;
        preview.SavedTimeMs = playhead.TimeMs;
        preview.SavedPlaying = playhead.Playing;

        playhead.TimeMs = start;
        playhead.SeekRequestMs = start;
        playhead.Playing = true;
    }

    private static void EndPreview(ref Playhead playhead, ref EditorPreview preview)
    {
        if (!preview.Active)
            return;

        playhead.Playing = preview.SavedPlaying;
        playhead.SeekRequestMs = preview.SavedTimeMs;
        preview.Active = false;
    }

    private static void LoadSongById(
        string id,
        ref LevelInfo level,
        ref Playhead playhead)
    {
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

    private static void LoadSong(
        string path,
        ref LevelInfo level,
        ref Playhead playhead)
    {
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

    private void MoveSelection(int direction, Entity editor)
    {
        if (direction == 0 ||
            !editor.HasComponent<NoteSelection>() ||
            !editor.HasComponent<Playhead>())
        {
            return;
        }

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        var items = new List<Entity>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (EditorItems.IsItem(entity) &&
                EditorItemTiming.TryGet(entity, out _))
            {
                items.Add(entity);
            }
        }

        if (items.Count == 0)
            return;

        Playhead playhead = editor.GetComponent<Playhead>();
        double step = EditorTiming.StepMs(playhead);
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        EditorSnapshot before = _history.Capture();
        bool changed = false;

        foreach (Entity entity in items)
        {
            if (!EditorItemTiming.TryGet(entity, out long timeMs))
                continue;

            long moved = Math.Clamp(
                timeMs + (long)Math.Round(step) * Math.Sign(direction),
                0,
                maxTime);
            if (moved == timeMs)
                continue;

            EditorItemTiming.Set(entity, moved);
            changed = true;
        }

        if (!changed)
            return;

        _history.Record(before, _history.Capture());
    }

    private static void ZoomTimeline(int direction, Entity editor)
    {
        if (!editor.HasComponent<EditorTimeline>())
            return;

        ref EditorTimeline timeline = ref editor.GetComponent<EditorTimeline>();
        timeline.Zoom = EditorTimelineZoom.Step(timeline.Zoom, direction);
        timeline.AutoZoomed = true;
    }

    private void SetTarget(int value, Entity editor)
    {
        int index = Math.Clamp(value, 0, KeybindSettings.Actions.Length - 1);
        KeybindAction target = KeybindSettings.Actions[index];

        if (editor.HasComponent<EditorNoteTarget>())
            editor.GetComponent<EditorNoteTarget>().Target = target;

        if (!editor.HasComponent<NoteSelection>())
            return;

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        var notes = new List<Entity>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (!entity.IsNull && entity.HasComponent<Note>())
                notes.Add(entity);
        }

        if (notes.Count == 0)
            return;

        EditorSnapshot before = _history.Capture();
        bool changed = false;

        foreach (Entity entity in notes)
        {
            ref Note note = ref entity.GetComponent<Note>();
            if (note.Target == target)
                continue;

            note.Target = target;
            changed = true;
        }

        if (!changed)
            return;

        _history.Record(before, _history.Capture());
    }

    private void CopySelection(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        _clipboard.Clear();
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!EditorItems.IsItem(entity))
                continue;

            if (entity.HasComponent<Note>())
            {
                Note note = entity.GetComponent<Note>();
                _clipboard.Add(new ClipboardItem(note.TimeMs, note, null, null));
            }
            else if (entity.HasComponent<Mine>())
            {
                Mine mine = entity.GetComponent<Mine>();
                _clipboard.Add(new ClipboardItem(mine.TimeMs, null, mine, null));
            }
            else if (entity.HasComponent<StartPoint>())
            {
                StartPoint start = entity.GetComponent<StartPoint>();
                _clipboard.Add(new ClipboardItem(start.TimeMs, null, null, start));
            }
        }
    }

    private void PasteSelection(Entity editor, ref Playhead playhead)
    {
        if (_clipboard.Count == 0 || !editor.HasComponent<NoteSelection>())
            return;

        long anchor = _clipboard.Min(item => item.TimeMs);
        long target = Math.Max(0, playhead.TimeMs);
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        EditorSnapshot before = _history.Capture();
        var created = new List<Entity>();

        foreach (ClipboardItem item in _clipboard)
        {
            long time = Math.Clamp(
                target + (item.TimeMs - anchor),
                0,
                maxTime);
            if (item.Note is { } note)
            {
                note.TimeMs = time;
                created.Add(EditorItems.Create(_entities, note));
            }
            else if (item.Mine is { } mine)
            {
                mine.TimeMs = time;
                created.Add(EditorItems.Create(_entities, mine));
            }
            else if (item.StartPoint is { } start)
            {
                start.TimeMs = time;
                created.Add(EditorItems.Create(_entities, start));
            }
        }

        if (created.Count == 0)
            return;

        editor.GetComponent<NoteSelection>().SetMany(created);
        _history.Record(before, _history.Capture());
    }

    private void DeleteSelection(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        var items = new List<Entity>();
        foreach (Entity entity in selection.AllNotes)
        {
            if (EditorItems.IsItem(entity))
                items.Add(entity);
        }

        if (items.Count == 0)
        {
            selection.Clear();
            return;
        }

        EditorSnapshot before = _history.Capture();
        foreach (Entity entity in items)
            entity.DeleteEntity();

        _history.Record(before, _history.Capture());
        selection.Clear();
    }

    private void ApplyLevel(
        LevelDocument document,
        string path,
        Entity editor,
        ref EditorPreview preview,
        ref EditorMetronome metronome)
    {
        document.Notes ??= new();
        document.Mines ??= new();
        document.StartPoints ??= new();
        document.Notes.RemoveAll(note => note is null);

        preview.Active = false;
        metronome.Enabled = false;
        _history.Clear();
        LevelDocumentMapper.Apply(_entities, editor, document);

        if (editor.HasComponent<EditorTimeline>())
            editor.GetComponent<EditorTimeline>().AutoZoomed = false;

        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();

        SetLevelFile(editor, path);
    }

    private static void SetLevelFile(Entity editor, string path)
    {
        if (editor.HasComponent<LevelFile>())
            editor.GetComponent<LevelFile>().Path = path;
    }

    private static void ClearSelection(Entity editor)
    {
        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();
    }

    private readonly record struct ClipboardItem(
        long TimeMs,
        Note? Note,
        Mine? Mine,
        StartPoint? StartPoint);

    private static int NormalizeSnap(int snap) => snap switch
    {
        8 => 8,
        16 => 16,
        _ => 4,
    };
}
