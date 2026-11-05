using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs; using Xanadu.Timeline;
using Xanadu.Levels;
using Xanadu.Settings;
using Xanadu.Editor;
using Xanadu.Editor.Commands;

namespace Xanadu.Systems.Editor;

/// <summary>Single ECS writer for editor intents from the Godot UI.</summary>
public sealed partial class EditorCommandSystem :
    QuerySystem<LevelInfo, Playfield, Playhead, EditorMetronome>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly EditorHistory _history;
    private readonly List<ClipboardItem> _clipboard = new();
    private int _pasteCount;

    /// <summary>Edits that change existing entities, which cannot happen while the query runs.</summary>
    private readonly List<Action> _afterQuery = new();

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
                            ref metronome,
                    entity);
            }
        });

        foreach (Action edit in _afterQuery)
            edit();

        _afterQuery.Clear();
    }

    private void Apply(
        EditorCommand command,
        ref LevelInfo level,
        ref Playfield playfield,
        ref Playhead playhead,
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

            case EditorCommandType.SetPlacementMode:
                if (Enum.IsDefined(
                        typeof(EditorPlacementMode),
                        command.IntValue) &&
                    editor.HasComponent<EditorPlacement>())
                {
                    editor.GetComponent<EditorPlacement>().Mode =
                        (EditorPlacementMode)command.IntValue;
                }

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

            case EditorCommandType.LoadSong:
                LoadSong(command.Text ?? "", ref level, ref playhead);
                if (editor.HasComponent<TimelineView>())
                    editor.GetComponent<TimelineView>().AutoZoomed = false;
                break;

            case EditorCommandType.LoadSongById:
                LoadSongById(command.Text ?? "", ref level, ref playhead);
                if (editor.HasComponent<TimelineView>())
                    editor.GetComponent<TimelineView>().AutoZoomed = false;
                break;

            case EditorCommandType.DeleteSelection:
                DeleteSelection(editor);
                break;

            case EditorCommandType.RotateSelection:
                RotateSelection(editor);
                break;

            case EditorCommandType.FlipSelection:
                FlipSelection(editor, command.IntValue != 0);
                break;

            case EditorCommandType.CycleOneWay:
                CycleOneWay(editor);
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

            // Restoring a snapshot makes structural changes, so it waits for the query.
            case EditorCommandType.Undo:
                _afterQuery.Add(() =>
                {
                    if (_history.Undo())
                        ClearSelection(editor);
                });
                break;

            case EditorCommandType.Redo:
                _afterQuery.Add(() =>
                {
                    if (_history.Redo())
                        ClearSelection(editor);
                });
                break;

            case EditorCommandType.ApplyLevel:
                if (command.Document is null)
                    break;

                ApplyLevel(
                    command.Document,
                    command.Text ?? "",
                    editor,
                            ref metronome);
                break;

            case EditorCommandType.RecoverLevel:
                if (command.Document is null)
                    break;

                ApplyLevel(
                    command.Document,
                    command.Text ?? "",
                    editor,
                    ref metronome,
                    isOnDisk: false);
                break;

            case EditorCommandType.ImportShader:
                ImportShader(command, editor);
                break;

            case EditorCommandType.RemoveShader:
                RemoveShader(command.IntValue, editor);
                break;

            case EditorCommandType.AssignShader:
                AssignShader(command.IntValue, editor);
                break;

            case EditorCommandType.SetPlayerShader:
            case EditorCommandType.SetFieldShader:
                SetSpecialShader(command, editor);
                break;

            case EditorCommandType.SetBackgroundColor:
                level.Background = command.ColorValue;
                break;

            case EditorCommandType.SetOutsideColor:
                level.Outside = command.ColorValue;
                break;

            case EditorCommandType.SetFrameColor:
                level.Frame = command.ColorValue;
                break;

            case EditorCommandType.SetLevelFile:
                SetLevelFile(editor, command.Text ?? "");
                // Directly after the query, so the autosave system later in
                // this group sees the save and clears its recovery file. The
                // shared command buffer would only apply it next frame, after
                // a preview may already have switched the scene.
                _afterQuery.Add(() => editor.AddTag<EditorSaved>());
                break;
        }
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
        if (!editor.HasComponent<TimelineView>())
            return;

        ref TimelineView timeline = ref editor.GetComponent<TimelineView>();
        timeline.Zoom = TimelineZoom.Step(timeline.Zoom, direction);
        timeline.AutoZoomed = true;
        // The buttons zoom about the playhead, so any pointer pin lets go.
        timeline.Pinned = false;
    }

    private void SetTarget(int value, Entity editor)
    {
        int index = Math.Clamp(value, 0, KeybindSettings.Actions.Length - 1);
        KeybindAction target = KeybindSettings.Actions[index];

        if (editor.HasComponent<EditorPlacement>())
            editor.GetComponent<EditorPlacement>().Note = target;
    }

    private void CopySelection(Entity editor)
    {
        if (!editor.HasComponent<NoteSelection>())
            return;

        _clipboard.Clear();
        _pasteCount = 0;
        foreach (Entity entity in editor.GetComponent<NoteSelection>().AllNotes)
        {
            if (!EditorItems.IsItem(entity))
                continue;

            if (entity.HasComponent<Note>())
            {
                Note note = entity.GetComponent<Note>();
                _clipboard.Add(new ClipboardItem(note.TimeMs, note, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity)));
            }
            else if (entity.HasComponent<Mine>())
            {
                Mine mine = entity.GetComponent<Mine>();
                _clipboard.Add(new ClipboardItem(mine.TimeMs, null, mine, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity)));
            }
            else if (entity.HasComponent<StartPoint>())
            {
                StartPoint start = entity.GetComponent<StartPoint>();
                _clipboard.Add(new ClipboardItem(start.TimeMs, null, null, start, MaskOf(entity)));
            }
            else if (entity.HasComponent<Block>())
            {
                _clipboard.Add(new ClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Block: entity.GetComponent<Block>()));
            }
            else if (entity.HasComponent<Spike>())
            {
                _clipboard.Add(new ClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Spike: entity.GetComponent<Spike>()));
            }
            else if (entity.HasComponent<LevelText>())
            {
                _clipboard.Add(new ClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Text: entity.GetComponent<LevelText>()));
            }
            else if (entity.HasComponent<LevelPolygon>())
            {
                LevelPolygon polygon = entity.GetComponent<LevelPolygon>();
                polygon.Item = Xanadu.Items.XifCopy.Clone(polygon.Item ?? new Xanadu.Items.Xif());
                _clipboard.Add(new ClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Polygon: polygon));
            }
            else if (entity.HasComponent<GrassPatch>())
            {
                _clipboard.Add(new ClipboardItem(0, null, null, null, MaskOf(entity), ItemShader.Of(entity), SelfModulate.Packed(entity), Grass: entity.GetComponent<GrassPatch>()));
            }
        }
    }

    private void PasteSelection(Entity editor, ref Playhead playhead)
    {
        if (_clipboard.Count == 0 || !editor.HasComponent<NoteSelection>())
            return;

        // Geometry is not timed, so only timed items set the time anchor.
        long anchor = _clipboard.Exists(item => !item.IsGeometry)
            ? _clipboard.Where(item => !item.IsGeometry).Min(item => item.TimeMs)
            : 0;
        // The paste lands on the snap nearest the playhead, so a copy made on the
        // beat stays on the beat; the items keep their spacing from each other.
        long target = EditorTiming.Snap(Math.Max(0, playhead.TimeMs), playhead);
        long maxTime = playhead.DurationMs > 0
            ? playhead.DurationMs
            : long.MaxValue;
        EditorSnapshot before = _history.Capture();
        var created = new List<Entity>();
        _pasteCount++;

        foreach (ClipboardItem item in _clipboard)
        {
            if (item.IsGeometry)
            {
                Entity geometry = PasteGeometry(item, editor);
                if (!geometry.IsNull)
                    created.Add(geometry);
                continue;
            }

            long time = Math.Clamp(
                target + (item.TimeMs - anchor),
                0,
                maxTime);
            if (item.Note is { } note)
            {
                note.TimeMs = time;
                Entity pasted = EditorItems.Create(_entities, note, item.Shader);
                Subscribe(pasted, item.Groups);
                SelfModulate.Put(pasted, item.Modulate);
                created.Add(pasted);
            }
            else if (item.Mine is { } mine)
            {
                mine.TimeMs = time;
                Entity pasted = EditorItems.Create(_entities, mine, item.Shader);
                Subscribe(pasted, item.Groups);
                SelfModulate.Put(pasted, item.Modulate);
                created.Add(pasted);
            }
            else if (item.StartPoint is { } start)
            {
                start.TimeMs = time;
                start.Groups = item.Groups;
                Entity pasted = EditorItems.Create(_entities, start);
                Subscribe(pasted, item.Groups);
                created.Add(pasted);
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
        ref EditorMetronome metronome,
        bool isOnDisk = true)
    {
        document.Notes ??= new();
        document.Mines ??= new();
        document.StartPoints ??= new();
        document.Notes.RemoveAll(note => note is null);

        metronome.Enabled = false;
        _history.Clear();
        LevelDocumentMapper.Apply(_entities, editor, document);

        if (editor.HasComponent<TimelineView>())
            editor.GetComponent<TimelineView>().AutoZoomed = false;

        if (editor.HasComponent<NoteSelection>())
            editor.GetComponent<NoteSelection>().Clear();

        SetLevelFile(editor, path);

        // A level straight from disk is the clean state to measure edits against.
        if (isOnDisk)
            CommandBuffer.AddTag<EditorBaselineStale>(editor.Id);
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
        StartPoint? StartPoint,
        uint Groups,
        int Shader = 0,
        uint Modulate = SelfModulate.White,
        Block? Block = null,
        Spike? Spike = null,
        LevelText? Text = null,
        GrassPatch? Grass = null,
        LevelPolygon? Polygon = null)
    {
        /// <summary>Untimed items: pasted a grid cell along rather than at the playhead.</summary>
        public bool IsGeometry => Block is not null || Spike is not null || Text is not null || Grass is not null || Polygon is not null;
    }

    private static uint MaskOf(Entity entity) =>
        entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0u;

    private void Subscribe(Entity item, uint mask)
    {
        if (mask != 0)
            CommandBuffer.AddComponent(item.Id, new ItemGroups(mask));
    }

    private static int NormalizeSnap(int snap) => snap switch
    {
        8 => 8,
        16 => 16,
        _ => 4,
    };
}
