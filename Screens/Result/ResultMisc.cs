// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.Screens.Result
{
    public partial class ResultMisc : VBoxContainer
    {
        private readonly InvertResultText urText = new();

        public ResultMisc(EntityStore store)
        {

            AddChild(new InvertResultText {
                Text = "NOTES: " + store.Query<NoteEcs>().Count,
            });
            AddChild(new InvertResultText {
                Text = "COMBO: " + store.Query<NoteEcs>().Count,

            });

            AddThemeConstantOverride("separation", 0);

            AddChild(urText);
            urCounter(store);
        }

        private void urCounter(EntityStore store)
        {
            var deviations = new List<float>();
            store.Query<Judged>().ForEachEntity((ref Judged judged, Entity entity) =>
            {
                deviations.Add(judged.Deviation);
            });

            if (deviations.Count == 0)
            {
                urText.Text = "UR: N/A";
                return;
            }

            float mean = deviations.Average();
            float variance = deviations.Sum(d => (d - mean) * (d - mean)) / deviations.Count;
            float stdDev = (float)Math.Sqrt(variance);
            float ur = stdDev * 10;

            urText.Text = $"UR: {ur:F2}";
        }

        private partial class InvertResultText : ResultText
        {
            public InvertResultText()
            {
                AddThemeStyleboxOverride("normal", ResultStyles.INSET);

                LabelSettings.FontColor = Colors.Black;
                LabelSettings.OutlineSize = 0;
                LabelSettings.FontSize = 20;
            }
        }
    }
}
