using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Levels;

namespace Xanadu.Editor;

/// <summary>
/// How the editor was left when a preview began. Returning rebuilds the editor
/// from its document, so this puts the view, tool settings, snap, metronome,
/// layer toggles and selection back. Items are remembered by what they are,
/// since their entities do not survive.
/// </summary>
public sealed class EditorReturnState
{
    private readonly record struct ItemKey(int Kind, long TimeMs, Vector2 Position);

    private PlayfieldView _view;
    private bool _hasTimeline;
    private TimelineView _timeline;
    private bool _hasPlacement;
    private EditorPlacement _placement;
    private int _snapDivision;
    private bool _metronome;
    private bool _hasLayers;
    private EditorLayers _layers;
    private bool _hasGroups;
    private EditorGroupState _groups;
    private readonly List<ItemKey> _selection = new();

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
            _metronome = editor.TryGetComponent(out EditorMetronome metronome) && metronome.Enabled,
            _hasLayers = editor.TryGetComponent(out EditorLayers layers),
            _layers = layers,
            _hasGroups = editor.TryGetComponent(out EditorGroupState groups),
            _groups = groups,
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
        if (_hasLayers && editor.HasComponent<EditorLayers>())
            editor.GetComponent<EditorLayers>() = _layers;

        // The group overlay itself stays closed; what it points at and how it snaps carry over.
        if (_hasGroups && editor.HasComponent<EditorGroupState>())
        {
            ref EditorGroupState groups = ref editor.GetComponent<EditorGroupState>();
            groups.BarGroup = _groups.BarGroup;
            groups.SoloGroup = _groups.SoloGroup;
            groups.Snap = _groups.Snap;
        }

        if (_selection.Count == 0 || !editor.HasComponent<NoteSelection>())
            return;

        var keys = new HashSet<ItemKey>(_selection);
        var found = new List<Entity>();
        foreach (Entity item in entities.Query().AllTags(Tags.Get<EditorItem>()).ToEntityList())
        {
            if (TryKey(item, out ItemKey key) && keys.Contains(key))
                found.Add(item);
        }

        if (found.Count > 0)
            editor.GetComponent<NoteSelection>().SetMany(found);
    }

    /// <summary>
    /// A start at the playhead with the player where the last preview's path had
    /// them then; failing that, at the last start before it, or else the field's
    /// bottom middle, where a level begins.
    /// </summary>
    public static StartPoint StartAtPlayhead(
        EntityStore entities,
        LevelDocument document,
        List<StartPoint> starts,
        long playheadMs)
    {
        Vector2 position = new(
            document.PlayfieldWidth / 2f,
            document.PlayfieldHeight - Player.ScaledSize(1f).Y / 2f);
        Vector2 direction = new(0, -1);
        foreach (StartPoint start in starts)
        {
            if (start.TimeMs <= playheadMs)
            {
                position = start.Position;
                direction = start.Direction;
            }
        }

        Entity editor = FindEditor(entities);
        if (!editor.IsNull && editor.TryGetComponent(out PlayerPath path))
        {
            foreach (PlayerPathSample sample in path.Samples)
            {
                if (sample.TimeMs <= playheadMs)
                    position = sample.Position;
            }
        }

        return new StartPoint(
            "",
            playheadMs,
            position,
            System.Numerics.Vector2.Zero,
            direction);
    }

    private static Entity FindEditor(EntityStore entities)
    {
        foreach (Entity entity in entities.Query<PlayfieldMode, PlayfieldView>().ToEntityList())
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

        int kind = System.Array.IndexOf(ItemKinds.All, ItemKinds.Of(item));
        long time = EditorItemTiming.TryGet(item, out long at) ? at : 0;
        key = new ItemKey(kind, time, position);
        return true;
    }
}
