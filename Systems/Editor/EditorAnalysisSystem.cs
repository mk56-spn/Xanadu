using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Xanadu.Ecs;
using Xanadu.Editor.Analysis;
using Xanadu.Scoring;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Keeps the density graph and the unreachable-note list current. Reading the
/// notes is cheap, so every frame hashes them and the analysis itself only
/// reruns when that hash moves.
/// </summary>
public sealed class EditorAnalysisSystem(EntityStore entities)
    : QuerySystem<PlayfieldView, Playfield, Playhead, EditorAnalysis>
{
    /// <summary>How far from its time a note can still be hit.</summary>
    private static readonly float WindowMs = (float)Math.Max(
        PlayerMovementWindowMs,
        ScoreRules.JudgementDeviation(Judgement.Miss));

    private const float PlayerMovementWindowMs = 150f;

    private readonly List<AnalysedNote> _notes = new();

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref view, ref playfield, ref playhead, ref analysis, _) =>
        {
            _notes.Clear();
            int signature = HashCode.Combine(
                view.Scale,
                view.ScreenOrigin,
                playfield.Width,
                playfield.Height,
                playhead.DurationMs / 500);

            entities.Query<Note>().ForEachEntity((ref note, _) =>
            {
                _notes.Add(new AnalysedNote(
                    note.TimeMs,
                    note.Position,
                    note.Kind,
                    note.DashDirection));
                // Summed, so the order the query visits notes in does not matter.
                signature += HashCode.Combine(
                    note.TimeMs,
                    note.Position,
                    note.Kind,
                    note.DashDirection);
            });

            if (analysis.Density is not null && signature == analysis.Signature)
                return;

            analysis.Signature = signature;
            analysis.Density = LevelDensity.Compute(_notes, playhead.DurationMs);
            analysis.Issues = PlayabilityAnalyzer
                .Analyze(_notes, PlayabilityGeometry.For(view, playfield, WindowMs))
                .ToArray();
        });
    }
}
