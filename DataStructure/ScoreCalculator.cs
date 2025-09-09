// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.DataStructure
{
    public class ScoreCalculator
    {
        public Dictionary<Judgement, int> Judgements { get; } = new();
        public int MaxCombo { get; private set; }
        public float Accuracy { get; set; }
        public float UnstableRate { get; }
        public int TotalNotes { get; }
        public string Rank { get; }


        public ScoreCalculator(EntityStore store)
        {
            foreach (var judgementValue in Enum.GetValues<Judgement>())
            {
                Judgements[judgementValue] = 0;
            }

            var judgedNotes = new List<(NoteEcs note, Judged judged)>();
            store.Query<NoteEcs, Judged>().ForEachEntity((ref NoteEcs note, ref Judged judged, Entity entity) =>
            {
                judgedNotes.Add((note, judged));
                Judgements[judged.Judgement]++;
            });

            // Sort by timing to calculate combo
            judgedNotes.Sort((a, b) => a.note.TimingPoint.CompareTo(b.note.TimingPoint));

            int currentCombo = 0;
            foreach (var (_, judged) in judgedNotes)
            {
                if (judged.Judgement != Judgement.Miss)
                {
                    currentCombo++;
                }
                else
                {
                    if (currentCombo > MaxCombo)
                    {
                        MaxCombo = currentCombo;
                    }
                    currentCombo = 0;
                }
            }
            if (currentCombo > MaxCombo)
            {
                MaxCombo = currentCombo;
            }

            TotalNotes = store.Query<NoteEcs>().Count;

            // Calculate Accuracy
            if (TotalNotes > 0)
            {
                Accuracy = (Judgements[Judgement.FlawlessP] * 1.0f +
                            Judgements[Judgement.Flawless] * 1.0f +
                            Judgements[Judgement.Clean] * 0.8f +
                            Judgements[Judgement.Fair] * 0.7f +
                            Judgements[Judgement.Deficient] * 0.5f) / TotalNotes;
            }

            // Calculate Rank
            if (Accuracy >= 0.999f) Rank = "SSS";
            else if (Accuracy >= 0.99f) Rank = "SS";
            else if (Accuracy >= 0.98f) Rank = "S";
            else if (Accuracy >= 0.95f) Rank = "A";
            else if (Accuracy >= 0.93f) Rank = "B";
            else if (Accuracy >= 0.90f) Rank = "C";
            else Rank = "FAIL";

            // Calculate Unstable Rate
            var deviations = judgedNotes.Select(jn => jn.judged.Deviation).ToList();
            if (deviations.Count > 1)
            {
                float mean = deviations.Average();
                float variance = deviations.Sum(d => (d - mean) * (d - mean)) / deviations.Count;
                UnstableRate = (float)Math.Sqrt(variance) * 10;
            }
        }
    }
}
