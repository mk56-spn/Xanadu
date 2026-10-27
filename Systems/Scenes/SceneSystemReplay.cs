using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Replays;
using Xanadu.Scenes;
using SceneState = Xanadu.Ecs.SceneState;

namespace Xanadu.Systems.Scenes;

public sealed partial class SceneSystem
{
    private readonly ReplayStore? _replays;
    private Replay? _pendingReplay;
    private Replay? _lastReplay;
    private bool _watchingReplay;

    /// <summary>Plays a recorded replay of a level in the catalog; false if the level is gone.</summary>
    public bool StartReplay(Replay replay)
    {
        LevelEntry? entry = _loadLevels()
            .FirstOrDefault(level => level.Document.Id == replay.LevelId);
        if (entry is null)
            return false;

        _lastReplay = replay;
        StartPlay(entry);
        _pendingReplay = replay;
        return true;
    }

    /// <summary>Watches the replay saved from the play that just finished.</summary>
    public bool WatchLastReplay() => _lastReplay is not null && StartReplay(_lastReplay);

    /// <summary>
    /// Stores the finished play's key log. A play with no recorder, such as a
    /// practice run from a start point, leaves nothing to watch.
    /// </summary>
    private void SaveReplay(LevelEntry entry, PlayResults results)
    {
        _lastReplay = null;
        foreach (Entity entity in _entities.Query<ReplayRecorder>().ToEntityList())
        {
            var replay = new Replay(
                entry.Document.Id,
                entry.Document.Name,
                entry.Document.SongId,
                _profiles.ActiveProfile?.Name ?? "",
                DateTimeOffset.UtcNow,
                entry.Document.Notes.Count,
                new ReplayResult(
                    results.Score,
                    results.Accuracy,
                    results.MaxCombo,
                    results.NotesHit,
                    results.TotalNotes,
                    results.FlawlessPCount,
                    results.FlawlessCount,
                    results.CleanCount,
                    results.FairCount,
                    results.DeficientCount,
                    results.TerribleCount,
                    results.MissCount),
                entity.GetComponent<ReplayRecorder>().Events.ToArray());

            try
            {
                _replays?.Save(replay);
                _lastReplay = replay;
            }
            catch (Exception exception)
            {
                GD.PushError($"Could not save replay: {exception.Message}");
            }

            return;
        }
    }
}
