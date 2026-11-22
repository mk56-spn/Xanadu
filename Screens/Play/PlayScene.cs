using Xanadu.Storage.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Levels;
using Xanadu.Model.Replays;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Playfields;
using Xanadu.Logic.Editor;

namespace Xanadu.Screens.Play;

/// <summary>Reuses the editor playfield to play a selected level.</summary>
public static class PlayScene
{

    public static void Build(
        EntityStore entities,
        Entity root,
        LevelEntry entry,
        KeybindSettings keybinds,
        IVisualItemFactory? visuals = null,
        long? startMs = null,
        Replay? replay = null,
        bool record = false,
        bool preview = false)
    {
        LevelDocument document = entry.Document;
        LoadedSong? song = entry.Song;
        Vector2 screen = UiStage.Full.Size;

        // The whole screen (the 1920 x 1080 design stage, scaled to the window)
        // is the view, and the whole field fits it: the camera stands still.
        var viewport = new Rect2(Vector2.Zero, screen);
        float scale = Math.Min(
            screen.X / Math.Max(1, document.PlayfieldWidth),
            screen.Y / Math.Max(1, document.PlayfieldHeight));

        float fieldWidth = document.PlayfieldWidth * scale;
        float fieldHeight = document.PlayfieldHeight * scale;
        var origin = new Vector2(
            (screen.X - Math.Min(fieldWidth, viewport.Size.X)) / 2,
            (screen.Y - Math.Min(fieldHeight, viewport.Size.Y)) / 2);
        Vector2 playerSize = Player.ScaledSize(scale);

        Entity playfieldEntity = entities.CreateEntity(
            new LevelInfo(document.Name, song),
            new LevelFile(entry.Path),
            new Playfield(document.PlayfieldWidth, document.PlayfieldHeight),
            new Playhead(
                timeMs: 0,
                bpm: song?.Bpm ?? 120,
                offsetMs: song?.OffsetMs ?? 0,
                playing: false),
            new PlayfieldView(origin, scale, screen),
            new PlayfieldMode(PlayfieldKind.Play),
            new Player(playerSize),
            // The curve player puts the square at the first curve note's head on its first frame.
            new Position(new Vector2(
                origin.X + fieldWidth / 2,
                origin.Y + fieldHeight / 2)));

        playfieldEntity.AddComponent(new PlayCamera(viewport));
        playfieldEntity.AddComponent(new BodyPunch());
        if (preview)
            playfieldEntity.AddComponent(new BodyTrace());
        root.AddChild(playfieldEntity);

        LevelDocumentMapper.Apply(entities, playfieldEntity, document, song);
        PlayChart.Load(entities);

        if (startMs is long from)
        {
            ref Playhead playhead = ref playfieldEntity.GetComponent<Playhead>();
            playhead.TimeMs = from;
            playhead.SeekRequestMs = from;
        }

        PlayHudBuilder.Build(
            entities, root, playfieldEntity, screen, document, song, origin, viewport,
            fieldHeight, visuals, startMs ?? 0);

        if (replay is not null)
            playfieldEntity.AddComponent(new ReplayPlayer(replay));
        else if (record)
            playfieldEntity.AddComponent(ReplayRecorder.Create());
    }
}
