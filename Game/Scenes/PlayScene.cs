using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Strings;
using Position = Xanadu.Core.Position;
using Xanadu.Model.Levels;
using Xanadu.Model.Replays;
using Xanadu.Model.Settings;
using Xanadu.Systems.Playfields;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>Reuses the editor playfield to play a selected level.</summary>
public static class PlayScene
{
    private const float TopInset = 92;
    private const float BottomInset = 54;
    internal const float SideInset = 40;

    /// <summary>The field size, in units, that would fit the screen whole before <see cref="PlayZoom"/>.</summary>
    public const float ViewSize = 2000;

    /// <summary>How far the play view is zoomed in past fitting a ViewSize field whole.</summary>
    public const float PlayZoom = 1.3f;

    public static void Build(
        EntityStore entities,
        Entity root,
        LevelEntry entry,
        KeybindSettings keybinds,
        IVisualRidFactory? visuals = null,
        long? startMs = null,
        Replay? replay = null,
        bool record = false,
        bool preview = false)
    {
        LevelDocument document = entry.Document;
        LoadedSong? song = entry.Song;
        Vector2 screen = UiStage.Full.Size;

        // A field up to ViewSize units fits the screen whole, then the view
        // zooms in by PlayZoom; whatever no longer fits, the camera follows
        // the player across.
        var viewport = new Rect2(
            SideInset,
            TopInset,
            screen.X - SideInset * 2,
            screen.Y - TopInset - BottomInset);
        float logicalSize = Mathf.Max(1, Mathf.Min(ViewSize, Mathf.Max(
            document.PlayfieldWidth,
            document.PlayfieldHeight)));
        float available = Mathf.Min(viewport.Size.X, viewport.Size.Y);
        float renderedSize = Mathf.Min(logicalSize, Mathf.Max(1, available));
        float scale = renderedSize / logicalSize * PlayZoom;

        float fieldWidth = document.PlayfieldWidth * scale;
        float fieldHeight = document.PlayfieldHeight * scale;
        var origin = new Vector2(
            (screen.X - Mathf.Min(fieldWidth, viewport.Size.X)) / 2,
            TopInset + (viewport.Size.Y - Mathf.Min(fieldHeight, viewport.Size.Y)) / 2);
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
