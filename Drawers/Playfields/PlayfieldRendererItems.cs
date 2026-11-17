using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Systems.Canvas;
using static Xanadu.Model.Playfields.PlayfieldMetrics;

namespace Xanadu.Drawers.Playfields;

/// <summary>What drawing a frame's items needs, the same for the editor and play.</summary>
/// <param name="Editor">The editor's view, which draws selection (<paramref name="IsSelected"/>, <paramref name="SelectedNote"/>).</param>
public readonly record struct PlayfieldItemFrame(
    PlayfieldView View,
    Playfield Playfield,
    Playhead Playhead,
    float Seconds,
    bool Editor,
    Func<Entity, bool>? IsSelected = null,
    Entity SelectedNote = default);

/// <summary>
/// Draws the floor, every note and the effects hits leave, then the player:
/// one order for the editor and play. Notes draw latest first, so the next one
/// due is on top; a route note's route goes under its own head.
/// </summary>
public static partial class PlayfieldRenderer
{
    private static readonly List<(long TimeMs, int Id)> NoteOrder = new();

    public static void DrawItems(
        IRectCanvas canvas,
        Handle rid,
        EntityStore entities,
        in PlayfieldItemFrame frame,
        Action? drawWedge)
    {
        DrawFloor(canvas, rid, frame.View, frame.Playfield);

        NoteOrder.Clear();
        entities.Query<Note>().EachEntity(new CollectNotes(NoteOrder));
        NoteOrder.Sort(static (a, b) => b.TimeMs.CompareTo(a.TimeMs));
        foreach ((_, int id) in NoteOrder)
            DrawNoteItem(canvas, rid, entities.GetEntityById(id), frame);

        Rect2 field = FieldRect(frame.View, frame.Playfield);
        entities.Query<NotePop>().EachEntity(new DrawPops(canvas, rid, entities, field));
        entities.Query<HitTrail>().EachEntity(new DrawTrails(canvas, rid, entities));

        drawWedge?.Invoke();
    }

    private static void DrawNoteItem(IRectCanvas canvas, Handle rid, Entity entity, in PlayfieldItemFrame frame)
    {
        bool selected = frame.IsSelected?.Invoke(entity) == true;
        Note note = entity.GetComponent<Note>();

        // A hit dash has done its job: it is gone from the field.
        if (note.IsDash && entity.Tags.Has<NoteSteering>())
            return;
        if (note.IsDash && frame.Editor)
            DrawDashReach(canvas, rid, note, frame.View, frame.Playhead, selected);
        if (note.IsHold)
            DrawHoldBar(canvas, rid, entity, frame.View, frame.Playhead, selected);

        if (note.IsRoute)
        {
            DrawRoute(canvas, rid, entity, frame.View, frame.Playhead, selected, frame.Editor);

            // Once the head is hit, only the route it steers along is left.
            if (entity.Tags.Has<NoteSteering>())
                return;
        }

        DrawNote(canvas, rid, entity, frame.View, frame.Playhead, frame.SelectedNote);
    }

    private readonly struct DrawPops(IRectCanvas canvas, Handle rid, EntityStore store, Rect2 field) : IEachEntity<NotePop>
    {
        public void Execute(ref NotePop pop, int id) => DrawNotePop(canvas, rid, store.GetEntityById(id), field);
    }

    private readonly struct DrawTrails(IRectCanvas canvas, Handle rid, EntityStore store) : IEachEntity<HitTrail>
    {
        public void Execute(ref HitTrail trail, int id) => DrawHitTrail(canvas, rid, store.GetEntityById(id));
    }

    /// <summary>Every note's time and id, to sort into draw order.</summary>
    private readonly struct CollectNotes(List<(long TimeMs, int Id)> into) : IEachEntity<Note>
    {
        public void Execute(ref Note note, int id) => into.Add((note.TimeMs, id));
    }
}
