using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Ecs;
using Xanadu.Model.Ecs.Components;
using Xanadu.Editor;
using Xanadu.Editor.History;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Model.Editor;
using Xanadu.Model.Playfields;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Dragging one point of the primary selected curve note's path, so a placed
/// curve can be reshaped; the rest stays put, and dragging the first point
/// moves the head with it. One undo step per drag.
/// </summary>
public sealed class EditorCurvePointTool(EditorSceneQuery scene, EditorHistory history)
{
    /// <summary>A press this close to a point, in screen pixels, takes it.</summary>
    private const float GrabRadius = 12f;

    private Entity _entity;
    private int _point;
    private EditorSnapshot _before;

    public bool Active => !_entity.IsNull;

    /// <summary>Starts a drag when the press lands on a point of the primary selected curve; true when it did.</summary>
    public bool TryBegin(Vector2 screenPosition, Entity editor, PlayfieldView view)
    {
        if (!editor.TryGetComponent(out NoteSelection selection))
            return false;

        Entity primary = selection.Note;
        if (!EditorItems.IsItem(primary) || !primary.TryGetComponent(out Note note) || note.Path is not { } path)
            return false;

        // The points sit where the curve's groups show it.
        view = GroupOffset.View(primary, view);
        int best = -1;
        float bestDistance = GrabRadius;
        for (int i = 0; i < path.Count; i++)
        {
            float distance = view.PlayfieldToScreen(path.Points[i]).DistanceTo(screenPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        if (best < 0)
            return false;

        _entity = primary;
        _point = best;
        _before = history.Capture();
        return true;
    }

    public void Update(Vector2 screenPosition, PlayfieldView view, Playfield playfield)
    {
        if (!EditorItems.IsItem(_entity) || !_entity.HasComponent<Note>())
            return;

        Vector2 point = GroupOffset.View(_entity, view).ScreenToPlayfield(screenPosition);
        point = new Vector2(
            System.Math.Clamp(point.X, 0, playfield.Width),
            System.Math.Clamp(point.Y, 0, playfield.Height));
        if (ViewportSnap.IsOn(scene.Store))
            point = PlayfieldGrid.NearestCrossing(point, playfield);

        ref Note note = ref _entity.GetComponent<Note>();
        if (note.Path is not { } path || _point >= path.Count)
            return;
        note.Path = path.WithPoint(_point, point);
        if (_point == 0)
            note.Position = point;
    }

    public void End()
    {
        history.Record(_before, history.Capture());
        _entity = default;
    }

    public void Cancel() => _entity = default;
}
