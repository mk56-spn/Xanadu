using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core.Timeline;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Kinds;
using Xanadu.Model.Levels;
using Xanadu.Model.Editor;

namespace Xanadu.Logic.Editor;

/// <summary>
///     How the editor was left when a preview began. Returning rebuilds the editor
///     from its document, so this puts the view, tool settings, snap, metronome
///     and selection back. Items are remembered by what they are,
///     since their entities do not survive.
/// </summary>
public sealed class EditorReturnState
{
    private readonly List<ItemKey> _selection = new();
    private bool _hasPlacement;
    private bool _hasTimeline;
    private bool _metronome;
    private EditorPlacement _placement;
    private int _snapDivision;
    private TimelineView _timeline;

    private PlayfieldView _view;

    public static EditorReturnState? Capture(EntityStore entities)
    {
        Entity editor = FindEditor(entities);
        if (editor.IsNull)
            return null;

        var state = new EditorReturnState
        {
            _view = editor.GetComponent<PlayfieldView>(),
            _hasTimeline = editor.TryGetComponent(out TimelineView timeline),
            _timeline = timeline,
            _hasPlacement = editor.TryGetComponent(out EditorPlacement placement),
            _placement = placement,
            _snapDivision = editor.TryGetComponent(out Playhead playhead) ? playhead.SnapDivision : 0,
            _metronome = editor.TryGetComponent(out EditorMetronome metronome) && metronome.Enabled
        };

        if (editor.TryGetComponent(out NoteSelection selection))
        {
            foreach (Entity item in selection.AllNotes)
            {
                if (EditorItems.IsItem(item) && TryKey(item, out ItemKey key))
                    state._selection.Add(key);
            }
        }

        return state;
    }

    public void Restore(EntityStore entities, Entity editor)
    {
        editor.GetComponent<PlayfieldView>() = _view;
        if (_hasTimeline && editor.HasComponent<TimelineView>())
            editor.GetComponent<TimelineView>() = _timeline;
        if (_hasPlacement && editor.HasComponent<EditorPlacement>())
            editor.GetComponent<EditorPlacement>() = _placement;
        if (_snapDivision > 0 && editor.HasComponent<Playhead>())
            editor.GetComponent<Playhead>().SnapDivision = _snapDivision;
        if (editor.HasComponent<EditorMetronome>())
            editor.GetComponent<EditorMetronome>().Enabled = _metronome;

        if (_selection.Count == 0 || !editor.HasComponent<NoteSelection>())
            return;

        var keys = new HashSet<ItemKey>(_selection);
        var found = new List<Entity>();
        foreach (Entity item in entities.Query().AllTags(Tags.Get<EditorItem>()).Entities)
        {
            if (TryKey(item, out ItemKey key) && keys.Contains(key))
                found.Add(item);
        }

        if (found.Count > 0)
            editor.GetComponent<NoteSelection>().SetMany(found);
    }

    private static Entity FindEditor(EntityStore entities)
    {
        foreach (Entity entity in entities.Query<PlayfieldMode, PlayfieldView>().Entities)
        {
            if (entity.GetComponent<PlayfieldMode>().Kind == PlayfieldKind.Editor)
                return entity;
        }

        return default;
    }

    private static bool TryKey(Entity item, out ItemKey key)
    {
        key = default;
        if (!EditorItemPosition.TryGet(item, out Vector2 position))
            return false;

        int kind = Array.IndexOf(ItemKinds.All, ItemKinds.Of(item));
        long time = EditorItemTiming.TryGet(item, out long at) ? at : 0;
        key = new ItemKey(kind, time, position);
        return true;
    }

    private readonly record struct ItemKey(int Kind, long TimeMs, Vector2 Position);
}
