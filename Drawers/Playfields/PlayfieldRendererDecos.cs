using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Levels;
using Xanadu.Core.Maths;
using Xanadu.Model.Animation;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Playfields;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldColors;

namespace Xanadu.Drawers.Playfields;

/// <summary>
/// Decos (<see cref="Deco"/>): each one's Xif drawn by <see cref="XifDrawer"/>
/// at its place on the field, with what its groups lay over it on the song's beat. Selected in the editor, its box is outlined.
/// </summary>
public static partial class PlayfieldRenderer
{
    public static void DrawDecos(IRectCanvas canvas, Handle rid, EntityStore entities, in PlayfieldItemFrame frame)
    {
        // What the groups lay over each deco now, once a frame, on the song's beat.
        Dictionary<string, GroupLayer>? layers = null;
        foreach (Entity level in entities.Query<LevelGroups>().ToEntityList())
        {
            ref LevelGroups groups = ref level.GetComponent<LevelGroups>();
            if (groups.Groups.Count == 0)
                break;
            LevelItemFields.Evaluate(groups.Groups, frame.Playhead.Tempo.BeatAt(frame.Playhead.ShownMs), groups.Layers);
            layers = groups.Layers;
            break;
        }
        entities.Query<Deco>().EachEntity(new DrawDecoEach(canvas, rid, entities, frame, layers));
    }

    /// <summary>One deco at its place; <paramref name="alpha"/> fades it (the editor's ghost).</summary>
    public static void DrawDeco(IRectCanvas canvas, Handle rid, in Deco deco, PlayfieldView view, bool selected, float alpha = 1f) =>
        DrawDeco(canvas, rid, deco, view, selected, GroupLayer.None, alpha);

    /// <summary>One deco with what its groups lay over it: moved, turned and sized about its origin, and faded.</summary>
    public static void DrawDeco(IRectCanvas canvas, Handle rid, in Deco deco, PlayfieldView view, bool selected, GroupLayer layer, float alpha = 1f)
    {
        Vector2 at = view.PlayfieldToScreen(deco.Position + layer.Offset);
        var placement = new Transform2D(layer.Rotation, new Vector2(view.Scale * layer.Scale, view.Scale * layer.Scale), 0f, at);
        XifDrawer.Shared.DrawNode(canvas, rid, deco.Xif, placement, view.Scale * layer.Scale, XifTint.Faded(alpha * layer.Opacity));

        if (selected)
        {
            Rect2 box = Decos.Bounds(deco);
            canvas.DrawBorder(rid, new Rect2(view.PlayfieldToScreen(box.Position), box.Size * view.Scale).Grow(4f), SelectedNoteColor, 2f);
        }
    }

    private readonly struct DrawDecoEach(IRectCanvas canvas, Handle rid, EntityStore store, PlayfieldItemFrame frame,
        Dictionary<string, GroupLayer>? layers) : IEachEntity<Deco>
    {
        public void Execute(ref Deco deco, int id)
        {
            bool selected = frame.Editor &&
                ((!frame.SelectedNote.IsNull && frame.SelectedNote.Id == id) ||
                 (frame.IsSelected is { } isSelected && isSelected(store.GetEntityById(id))));
            GroupLayer layer = layers is not null && layers.TryGetValue(deco.Xif.Id, out GroupLayer found) ? found : GroupLayer.None;
            DrawDeco(canvas, rid, deco, frame.View, selected, layer);
        }
    }
}
