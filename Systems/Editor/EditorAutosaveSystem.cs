using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Ecs;
using Xanadu.Editor.Recovery;
using Xanadu.Levels;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Copies unsaved edits to a recovery file every few seconds, and when the
/// editor opens a level that has such a copy, offers it back. "Unsaved" means
/// different from the state the level was loaded or last saved in, so an
/// undone edit leaves nothing behind.
/// </summary>
public sealed class EditorAutosaveSystem(
    EntityStore entities,
    LevelRecoveryStore store,
    Func<DateTimeOffset>? clock = null,
    float intervalSeconds = EditorAutosaveSystem.DefaultIntervalSeconds)
    : QuerySystem<LevelFile, EditorUiRequest>
{
    public const float DefaultIntervalSeconds = 20f;

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    private long _editor = -1;
    private string _baseline = "";
    private string? _lastWritten;
    private string _path = "";
    private float _elapsed;

    protected override void OnUpdate()
    {
        CommandBuffer buffer = CommandBuffer;
        float delta = Tick.deltaTime;

        Query.ForEachEntity((ref file, ref _, entity) =>
        {
            if (entity.Pid != _editor)
            {
                // A new editor scene: whatever it loaded is the clean state.
                _editor = entity.Pid;
                Rebase(entity, buffer, file.Path, offer: true);
                return;
            }

            if (entity.Tags.Has<EditorBaselineStale>())
            {
                buffer.RemoveTag<EditorBaselineStale>(entity.Id);
                Rebase(entity, buffer, file.Path, offer: true);
                return;
            }

            if (entity.Tags.Has<EditorSaved>())
            {
                buffer.RemoveTag<EditorSaved>(entity.Id);
                store.Delete(_path);
                store.Delete(file.Path);
                Rebase(entity, buffer, file.Path, offer: false);
                return;
            }

            _elapsed += delta;
            if (_elapsed < intervalSeconds)
                return;

            _elapsed = 0;
            Autosave(entity, file.Path);
        });
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

    private void Autosave(Entity editor, string path)
    {
        // Until the user answers, the file on disk is the thing they may still want.
        if (editor.HasComponent<EditorRecoveryOffer>())
            return;

        LevelDocument level = LevelDocumentMapper.Capture(entities, editor);
        string current = LevelRecoveryStore.Canonical(level);
        if (current == (_lastWritten ?? _baseline))
            return;

        if (current == _baseline)
        {
            store.Delete(path);
            _lastWritten = null;
            return;
        }

        store.Write(path, level, _clock());
        _lastWritten = current;
        _path = path;
    }
}
