using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Scoring;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// The run's chart for shaders (<see cref="ShaderChart"/>): loaded from the
/// level's notes when it starts, and each note's result written where it is
/// judged. Shaders read it as <c>notes.</c> in <c>xanadu.play</c>.
/// </summary>
public static class PlayChart
{
    /// <summary>Every note of the level, by time, into the chart; each note entity learns its place.</summary>
    public static void Load(EntityStore entities)
    {
        var notes = new List<Entity>(entities.Query<Note>().ToEntityList());
        notes.Sort(static (a, b) =>
        {
            int time = a.GetComponent<Note>().TimeMs.CompareTo(b.GetComponent<Note>().TimeMs);
            return time != 0 ? time : a.Id.CompareTo(b.Id);
        });

        var chart = new ChartNote[notes.Count];
        for (int i = 0; i < notes.Count; i++)
        {
            Note note = notes[i].GetComponent<Note>();
            Color colour = GroupColor.For(notes[i], PlayfieldRenderer.ColorFor(note));
            chart[i] = new ChartNote
            {
                Place = new Vector4(note.Position.X, note.Position.Y, 0, 0),
                Colour = new Vector4(colour.R, colour.G, colour.B, colour.A),
                Timing = new Vector4(note.TimeMs / 1000f, (float)note.Kind, (float)note.Target - (float)Settings.KeybindAction.Action1, i),
            };
            notes[i].AddComponent(new ChartIndex(i));
        }

        ShaderChart.Load(chart);
    }

    /// <summary>
    /// The note was hit (<paramref name="judgement"/>) or missed, <paramref name="offsetMs"/>
    /// early (as scoring measures it: the note's time less the press's), where it stood then.
    /// </summary>
    public static void Judge(Entity note, Judgement judgement, long offsetMs)
    {
        int index = ChartIndex.Of(note);
        if (index < 0)
            return;

        Note played = note.GetComponent<Note>();
        bool missed = judgement == Judgement.Miss;
        Vector2 place = GroupOffset.Shown(note, played.Position);
        ShaderChart.Judge(
            index,
            (played.TimeMs - offsetMs) / 1000f,
            (float)judgement,
            -offsetMs,
            missed ? ShaderChart.Missed : ShaderChart.Hit,
            place);
    }
}
