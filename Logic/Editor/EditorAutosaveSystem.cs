using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Model.Levels;
using Xanadu.Storage.Editor;

namespace Xanadu.Logic.Editor;

/// <summary>
/// Every few seconds, with autosave on, asks for unsaved edits to be saved to
/// the level's own file, silently. Either way it also copies them to a
/// recovery file, which is only for a crash: a save, leaving the editor or
/// quitting the game normally throws it away, so the editor only offers a
/// copy back after the game died with edits unsaved. "Unsaved" means
/// different from the state the level was loaded or last saved in, so an
/// undone edit leaves nothing behind.
/// </summary>
public sealed class EditorAutosaveSystem(
    EntityStore entities,
    LevelRecoveryStore store,
    Func<DateTimeOffset>? clock = null,
    float intervalSeconds = EditorAutosaveSystem.DefaultIntervalSeconds,
    Func<bool>? saveToFile = null)
    : QuerySystem<FilePath>
{
    public const float DefaultIntervalSeconds = 20f;

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    private long _editor = -1;
    private string _baseline = "";
    private string? _lastWritten;
    private string _path = "";
    private float _elapsed;
    private Entity _saveFrom;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        Query.EachEntity(new Watch(this, Query.Store, CommandBuffer, store, intervalSeconds, Tick.deltaTime));

        // Asked after the loop: the overlay, which owns writing level files, saves.
        if (!_saveFrom.IsNull)
            _saveFrom.EmitSignal(new SaveQuietly());
        _saveFrom = default;
    }

    /// <summary>
    /// The editor closed normally, so this session's autosave is not a crash
    /// to recover from. A copy from an earlier crash that is still waiting on
    /// an answer was never written by this session and stays.
    /// </summary>
    public void EndSession()
    {
        if (_lastWritten is null)
            return;

        store.Delete(_path);
        _lastWritten = null;
        _elapsed = 0;
    }

    private void Rebase(Entity editor, CommandBuffer buffer, string path, bool offer)
    {
        LevelDocument level = LevelDocumentMapper.Capture(entities, editor);
        _baseline = LevelRecoveryStore.Canonical(level);
        _lastWritten = null;
        _path = path;
        _elapsed = 0;

        if (!offer)
            return;

        LevelRecoveryStore.Entry? saved = store.Read(path);
        if (saved is null)
            return;

        if (LevelRecoveryStore.Canonical(saved.Level) == _baseline)
            store.Delete(path);
        else
            buffer.AddComponent(editor.Id, new EditorRecoveryOffer(path, saved.Level, saved.SavedAt));
    }

    /// <summary>Writes the crash copy; true while the level has unsaved edits.</summary>
    private bool Autosave(Entity editor, string path)
    {
        // Until the user answers, the file on disk is the thing they may still want.
        if (editor.HasComponent<EditorRecoveryOffer>())
            return false;

        LevelDocument level = LevelDocumentMapper.Capture(entities, editor);
        string current = LevelRecoveryStore.Canonical(level);
        if (current == _baseline)
        {
            if (_lastWritten is not null)
                store.Delete(path);
            _lastWritten = null;
            return false;
        }

        if (current != _lastWritten)
        {
            store.Write(path, level, _clock());
            _lastWritten = current;
            _path = path;
        }

        return true;
    }

    /// <summary>
    /// Hands the save to the overlay, which owns writing level files. A level
    /// with no file yet is left to the user to save.
    /// </summary>
    private void RequestSave(Entity editor, string path)
    {
        if (saveToFile?.Invoke() == true && !string.IsNullOrEmpty(path))
            _saveFrom = editor;
    }

    private readonly record struct Watch(EditorAutosaveSystem System, EntityStore Store, CommandBuffer Buffer, LevelRecoveryStore Recovery, float IntervalSeconds, float Delta) : IEachEntity<FilePath>
    {
        public void Execute(ref FilePath file, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (entity.Pid != System._editor)
            {
                // A new editor scene: whatever it loaded is the clean state.
                System._editor = entity.Pid;
                System.Rebase(entity, Buffer, file.Value, offer: true);
                return;
            }

            if (entity.Tags.Has<EditorBaselineStale>())
            {
                Buffer.RemoveTag<EditorBaselineStale>(id);
                System.Rebase(entity, Buffer, file.Value, offer: true);
                return;
            }

            if (entity.Tags.Has<EditorSaved>())
            {
                Buffer.RemoveTag<EditorSaved>(id);
                Recovery.Delete(System._path);
                Recovery.Delete(file.Value);
                System.Rebase(entity, Buffer, file.Value, offer: false);
                return;
            }

            System._elapsed += Delta;
            if (System._elapsed < IntervalSeconds)
                return;

            System._elapsed = 0;
            if (System.Autosave(entity, file.Value))
                System.RequestSave(entity, file.Value);
        }
    }
}
