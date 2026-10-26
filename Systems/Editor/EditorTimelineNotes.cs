using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using Writer.Timeline;

namespace Writer.Systems.Editor;

/// <summary>
/// Where each note sits on the editor timeline strip: a column per time, the
/// notes of one time stacked as small squares. Drawing, clicking and box
/// selecting all read the same squares, so they cannot disagree.
/// </summary>
public static class EditorTimelineNotes
{
    private const int MaxStack = 7;
    private const float Gap = 1f;

    /// <summary>The side of a note square at a timeline zoom.</summary>
    public static float SquareSize(float zoom) => Math.Clamp(6f + zoom * 0.0625f, 6f, 8f);

    /// <summary>The square of every note in the visible window, in screen space.</summary>
    public static List<(Entity Note, Rect2 Bounds)> Slots(
        EntityStore entities,
        Rect2 ruler,
        long windowStart,
        long windowEnd,
        float zoom)
    {
        var byTime = new Dictionary<long, List<Entity>>();
        foreach (Entity entity in entities.Query<Note>().ToEntityList())
        {
            long time = entity.GetComponent<Note>().TimeMs;
            if (time < windowStart || time > windowEnd)
                continue;

            if (!byTime.TryGetValue(time, out List<Entity>? notes))
            {
                notes = new List<Entity>();
                byTime.Add(time, notes);
            }

            notes.Add(entity);
        }

        float square = SquareSize(zoom);
        var window = new TimeWindow(windowStart, windowEnd);
        var slots = new List<(Entity, Rect2)>();
        foreach ((long time, List<Entity> notes) in byTime)
        {
            float x = window.XForTime(ruler, time);
            int visible = Math.Min(notes.Count, MaxStack);
            float height = visible * square + Math.Max(0, visible - 1) * Gap;
            float top = ruler.Position.Y + (ruler.Size.Y - height) / 2;
            for (int i = 0; i < visible; i++)
            {
                slots.Add((notes[i], new Rect2(
                    x - square / 2,
                    top + i * (square + Gap),
                    square,
                    square)));
            }
        }

        return slots;
    }
}
