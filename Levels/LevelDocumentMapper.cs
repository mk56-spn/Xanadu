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
            Name = level.Name,
            AudioPath = level.AudioPath,
            Artist = level.Artist,
            Difficulty = level.Difficulty,
            Bpm = playhead.Bpm,
            OffsetMs = playhead.OffsetMs,
            SnapDivision = playhead.SnapDivision,
            PlayfieldWidth = playfield.Width,
            PlayfieldHeight = playfield.Height,
            Notes = new List<NoteDocument>(),
            MovementEvents = new List<MovementEventDocument>(),
        };

        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            Note note = entity.GetComponent<Note>();
            document.Notes.Add(new NoteDocument
            {
                TimeMs = note.TimeMs,
                X = note.Position.X,
                Y = note.Position.Y,
                FadeInMs = note.FadeInMs,
                FadeOutMs = note.FadeOutMs,
                Target = note.Target,
            });
        }

        document.Notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));

        foreach (Entity entity in entities.Query<MovementEvent>().ToEntityList())
        {
            document.MovementEvents.Add(new MovementEventDocument
            {
                TimeMs = entity.GetComponent<MovementEvent>().TimeMs,
            });
        }

        document.MovementEvents.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return document;
    }

    public static void Apply(EntityStore entities, Entity editor, LevelDocument document)
    {
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
            entity.DeleteEntity();

        foreach (Entity entity in entities.Query<MovementEvent>().ToEntityList())
            entity.DeleteEntity();

        foreach (NoteDocument note in document.Notes)
        {
            entities.CreateEntity(new Note(
                note.TimeMs,
                new Vector2(note.X, note.Y),
                note.FadeInMs,
                note.FadeOutMs,
                note.Target));
        }

        foreach (MovementEventDocument movement in document.MovementEvents)
            entities.CreateEntity(new MovementEvent(movement.TimeMs));

        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.Name = document.Name;
        level.AudioPath = document.AudioPath;
        level.Artist = document.Artist;
        level.Difficulty = document.Difficulty;

        ref Playfield playfield = ref editor.GetComponent<Playfield>();
        playfield.Width = document.PlayfieldWidth;
        playfield.Height = document.PlayfieldHeight;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Bpm = document.Bpm;
        playhead.OffsetMs = document.OffsetMs;
        playhead.SnapDivision = document.SnapDivision;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = 0;
        playhead.AudioRevision++;
    }
}
