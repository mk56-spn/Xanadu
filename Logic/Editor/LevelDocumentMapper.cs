using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Logic.Editor.Kinds;
using Xanadu.Model.Levels;

namespace Xanadu.Logic.Editor;

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
        };

        // A brand-new level keeps the identity the document constructor made.
        if (level.LevelId.Length > 0)
            document.Id = level.LevelId;
        if (level.Slug.Length > 0)
            document.Slug = level.Slug;

        foreach (ItemKind kind in ItemKinds.All)
            kind.CaptureInto(entities, document);

        return document;
    }

    /// <summary>Applies a level document with its song already opened, as a <see cref="LevelEntry"/> carries it.</summary>
    public static void Apply(EntityStore entities, Entity editor, LevelDocument document, LoadedSong? song)
    {
        foreach (ItemKind kind in ItemKinds.All)
            kind.ApplyFrom(entities, document);

        ref LevelInfo level = ref editor.GetComponent<LevelInfo>();
        level.LevelId = document.Id ?? "";
        level.Slug = document.Slug ?? "";
        level.SongId = document.SongId;
        level.Name = document.Name;
        level.Song = song;
        level.Artist = song?.Artist ?? "";
        level.Difficulty = document.Name;

        ref Playfield playfield = ref editor.GetComponent<Playfield>();
        playfield.Width = document.PlayfieldWidth;
        playfield.Height = document.PlayfieldHeight;

        ref Playhead playhead = ref editor.GetComponent<Playhead>();
        playhead.Tempo = song?.Tempo() ?? TempoMap.Default;
        playhead.SnapDivision = document.SnapDivision;
        playhead.TimeMs = 0;
        playhead.DurationMs = 0;
        playhead.Playing = false;
        playhead.SeekRequestMs = 0;
        playhead.AudioRevision++;
    }
}
