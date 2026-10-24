using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;

namespace Writer.Levels;

/// <summary>Copies editor ECS state to and from the serializable document.</summary>
public static class LevelDocumentMapper
{
    public static LevelDocument Capture(EntityStore entities, Entity editor)
    {
        LevelInfo level = editor.GetComponent<LevelInfo>();
        Playfield playfield = editor.GetComponent<Playfield>();
        Playhead playhead = editor.GetComponent<Playhead>();

        var document = new LevelDocument
        {
            SongId = level.SongId,
            Name = level.Name,
            SnapDivision = playhead.SnapDivision,
            PlayfieldWidth = playfield.Width,
            PlayfieldHeight = playfield.Height,
            Notes = new List<NoteDocument>(),
            Mines = new List<MineDocument>(),
            StartPoints = new List<StartPointDocument>(),
        };

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            document.Notes.Add(new NoteDocument
            {
                TimeMs = note.TimeMs,
                X = note.Position.X,
                Y = note.Position.Y,
                Target = note.Target,
                Kind = note.Kind,
                DashDirection = note.DashDirection,
                ContactRequired = note.ContactRequired,
            });
        }

        document.Notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
        {
            Mine mine = entity.GetComponent<Mine>();
            document.Mines.Add(new MineDocument
            {
                TimeMs = mine.TimeMs,
                X = mine.Position.X,
                Y = mine.Position.Y,
                FadeInMs = mine.FadeInMs,
                FadeOutMs = mine.FadeOutMs,
            });
        }

        document.Mines.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        foreach (Entity entity in entities.Query<StartPoint>().ToEntityList())
        {
            StartPoint start = entity.GetComponent<StartPoint>();
            document.StartPoints.Add(new StartPointDocument
            {
                Name = start.Name,
                TimeMs = start.TimeMs,
                X = start.Position.X,
                Y = start.Position.Y,
                MomentumX = start.Momentum.X,
                MomentumY = start.Momentum.Y,
                DirectionX = start.Direction.X,
                DirectionY = start.Direction.Y,
            });
        }

        document.StartPoints.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return document;
    }

    public static void Apply(EntityStore entities, Entity editor, LevelDocument document)
    {
        document.Notes ??= new List<NoteDocument>();
        document.Mines ??= new List<MineDocument>();
        document.StartPoints ??= new List<StartPointDocument>();

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<Mine>().ToEntityList())
            entity.DeleteEntity();

        foreach (NoteDocument note in document.Notes)
        {
            EditorItems.Create(entities, new Note(
                note.TimeMs,
                new Vector2(note.X, note.Y),
                note.Target,
                note.Kind,
                note.DashDirection,
                note.ContactRequired));
        }

        foreach (Entity entity in entities.Query<StartPoint>().ToEntityList())
            entity.DeleteEntity();

        foreach (StartPointDocument start in document.StartPoints)
        {
            EditorItems.Create(entities, new StartPoint(
                start.Name,
                start.TimeMs,
                new Vector2(start.X, start.Y),
                new System.Numerics.Vector2(
                    start.MomentumX,
                    start.MomentumY),
                new Vector2(start.DirectionX, start.DirectionY)));
        }

        foreach (MineDocument mine in document.Mines)
        {
            EditorItems.Create(entities, new Mine(
                mine.TimeMs,
                new Vector2(mine.X, mine.Y),
                mine.FadeInMs,
                mine.FadeOutMs));
        }

        SongDocument? song = string.IsNullOrWhiteSpace(document.SongId)
            ? null
            : new SongStore().Find(document.SongId);

        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.SongId = document.SongId;
        level.Name = document.Name;
        level.AudioPath = song?.AudioPath ?? level.AudioPath;
        level.Artist = song?.Artist ?? "";
        level.Difficulty = document.Name;

        ref Playfield playfield = ref editor.GetComponent<Playfield>();
        playfield.Width = document.PlayfieldWidth;
        playfield.Height = document.PlayfieldHeight;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Bpm = song?.Bpm ?? 120;
        playhead.OffsetMs = song?.OffsetMs ?? 0;
        playhead.SnapDivision = document.SnapDivision;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = 0;
        playhead.AudioRevision++;
    }
}
