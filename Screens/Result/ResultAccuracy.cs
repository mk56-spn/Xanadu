// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;

namespace XanaduProject.Screens.Result
{
    public partial class ResultAccuracy : Label
    {
        public ResultAccuracy(EntityStore store)
        {
            int totalNotes = store.Query<NoteEcs>().Entities.Count();

            float acc = 0;
            foreach (var v in store.Query<NoteEcs>().Entities)
            {
                var judgement = v.GetComponent<Judged>().Judgement;
                switch (judgement)
                {
                    case Judgement.FlawlessP or Judgement.Flawless:
                        acc += 100f / totalNotes;
                        break;
                    case Judgement.Clean:
                        acc += 80f / totalNotes;
                        break;
                    case Judgement.Fair:
                        acc += 70f / totalNotes;
                        break;
                    case Judgement.Deficient:
                        acc += 50f / totalNotes;
                        break;
                }
            }
            Text = acc.ToString("0.00") + "%";

            var color= acc switch
            {
                > 98 => new Color(1, 1, 0),
                > 95 => new Color(0, 1, 0),
                > 90 => new Color(1, 1, 0),
                > 80 => new Color(1, 0.5f, 0),
                _ => new Color(1, 0, 0)
            };

            LabelSettings = new LabelSettings
            {
                FontColor = color,
                FontSize = 150,
                OutlineColor = Colors.White,
                OutlineSize = 5,
                Font = FontSource.PLASTIC
            };

            SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight, margin: 40);
        }
    }

}
