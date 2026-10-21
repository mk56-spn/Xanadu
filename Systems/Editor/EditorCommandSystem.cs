using System;
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
                break;

            case EditorCommandType.DeleteSelection:
                DeleteSelection(editor);
                break;

            case EditorCommandType.SetTarget:
                SetTarget(command.IntValue, editor);
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

    private static void LoadSong(
        string path,
        ref LevelInfo level,
        ref Playhead playhead)
    {
        level.AudioPath = path;
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

    private void SetTarget(int value, Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        if (selection.Note.IsNull || !selection.Note.HasComponent<Note>())
            return;

        int index = Math.Clamp(value, 0, KeybindSettings.Actions.Length - 1);
        KeybindAction target = KeybindSettings.Actions[index];
        ref Note note = ref selection.Note.GetComponent<Note>();
        if (note.Target == target)
            return;

        var before = _history.Capture();
        note.Target = target;
        var after = _history.Capture();
        _history.Record(before, after);
    }

    private void DeleteSelection(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        ref NoteSelection selection = ref editor.GetComponent<NoteSelection>();
        if (selection.Note.IsNull || !selection.Note.HasComponent<Note>())
        {
            selection.Note = default;
            return;
        }

        var before = _history.Capture();
        selection.Note.DeleteEntity();
        var after = _history.Capture();
        _history.Record(before, after);
        selection.Note = default;
    }

    private void ApplyLevel(
        LevelDocument document,
        string path,
        Entity editor,
        ref EditorPreview preview,
        ref EditorMetronome metronome)
    {
        document.Notes ??= new();
        document.Notes.RemoveAll(note => note is null);

        preview.Active = false;
        metronome.Enabled = false;
        _history.Clear();
        LevelDocumentMapper.Apply(_entities, editor, document);

        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Note = default;

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
            editor.GetComponent<NoteSelection>().Note = default;
    }

    private static int NormalizeSnap(int snap) => snap switch
    {
        8 => 8,
        16 => 16,
        _ => 4,
    };
}
