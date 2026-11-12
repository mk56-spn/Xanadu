using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Strings;
using Position = Xanadu.Ecs.Position;
using Xanadu.Levels;
using Xanadu.Replays;
using Xanadu.Settings;
using Xanadu.Scoring;
using Xanadu.Systems.Characters;
using Xanadu.Systems.Playfields;
using Xanadu.Theme;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Scenes;

/// <summary>Reuses the editor playfield to play a selected level.</summary>
public static class PlayScene
{
    private const float TopInset = 92;
    private const float BottomInset = 54;
    internal const float SideInset = 40;

    /// <summary>The most of the field, in units, the screen shows at once.</summary>
    public const float ViewSize = 1000;

    public static void Build(
        EntityStore entities,
        Entity root,
        LevelEntry entry,
        KeybindSettings keybinds,
        IVisualRidFactory? visuals = null,
        StartPoint? start = null,
        Replay? replay = null,
        bool record = false)
    {
        LevelDocument document = entry.Document;
        LoadedSong? song = entry.Song;
        Vector2 screen = UiStage.Full.Size;

        // A field up to ViewSize units fits the screen whole, as it always did;
        // a bigger one is shown at that same scale and the camera follows the
        // player across it.
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
        float scale = renderedSize / logicalSize;

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
            new Position(new Vector2(
                origin.X + fieldWidth / 2,
                origin.Y + fieldHeight - playerSize.Y / 2)),
            new Character(1f));

        playfieldEntity.AddComponent(new PlayCamera(viewport));
        playfieldEntity.AddComponent(LevelShaders.Create());
        root.AddChild(playfieldEntity);
        playfieldEntity.AddComponent(new PlayerCharacter(CharacterKind.Contact));
        playfieldEntity.AddComponent(new ContactCounters());
        playfieldEntity.AddComponent(new KeyTrail());
        string[] keyLabels = new string[KeybindSettings.Actions.Length];
        for (int i = 0; i < keyLabels.Length; i++)
        {
            keyLabels[i] = KeybindSettings.KeyLabel(
                keybinds.Get(KeybindSettings.Actions[i]));
        }
        playfieldEntity.AddComponent(new KeyTrailLabels(keyLabels));

        LevelDocumentMapper.Apply(entities, playfieldEntity, document, song);

        if (start is StartPoint point)
        {
            ref Playhead playhead = ref playfieldEntity.GetComponent<Playhead>();
            playhead.TimeMs = point.TimeMs;
            playhead.SeekRequestMs = point.TimeMs;
            Vector2 spawn = playfieldEntity.TryGetComponent(out LevelGroups levelGroups)
                ? SpawnPosition(levelGroups, point, playhead.Tempo)
                : point.Position;
            ref Position playerPosition = ref playfieldEntity.GetComponent<Position>();
            playerPosition.Value = playfieldEntity
                .GetComponent<PlayfieldView>()
                .PlayfieldToScreen(spawn);
        }

        PlayHudBuilder.Build(
            entities, root, playfieldEntity, screen, document, song, origin, viewport,
            fieldHeight, visuals, start?.TimeMs ?? 0);

        if (replay is not null)
            playfieldEntity.AddComponent(new ReplayPlayer(replay));
        else if (record)
            playfieldEntity.AddComponent(ReplayRecorder.Create());
    }

    /// <summary>Where the player starts: the start point plus its groups' move at its time.</summary>
    public static Vector2 SpawnPosition(LevelGroups groups, StartPoint start, Xanadu.Levels.TempoMap? tempo = null) =>
        start.Position + GroupModulation.Offset(groups.Groups, start.Groups, start.TimeMs / 1000f, tempo);
}
