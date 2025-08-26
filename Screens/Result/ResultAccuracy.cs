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

            LabelSettings = new LabelSettings
            {
                FontSize = 150,
                OutlineColor = Colors.White.Darkened(0.7F),
                OutlineSize = 20,
                Font = FontSource.PLASTIC
            };

            SetAnchorsAndOffsetsPreset(LayoutPreset.CenterRight, margin: 40);

            var separator = new ColorRect
            {
                Position = new Vector2(0, 160),
                Size = new Vector2(300, 5),
                Color = Colors.White.Darkened(0.5f)
            };
            AddChild(separator);

            var rankLabel = new Label
            {
                Text = acc switch
                {
                    >= 99.9f => "SSS",
                    >= 99f => "SS",
                    >= 98f => "S",
                    >= 95f => "A",
                    >= 93f => "B",
                    >= 90f => "C",
                    _ => "FAIL"
                },
                LabelSettings = new LabelSettings
                {
                    FontSize = 75,
                    OutlineColor = Colors.White.Darkened(0.7F),
                    OutlineSize = 15,
                    Font = FontSource.PLASTIC
                },
                Position = new Vector2(0, 180),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            rankLabel.Modulate = acc switch
            {
                >= 99.9f => new Color(0.9f, 0.9f, 1.0f), // Platinum
                >= 99f => Colors.Gold,
                >= 98f => Colors.Silver,
                >= 95f => new Color(0.8f, 0.5f, 0.2f), // Bronze
                _ => Colors.White
            };

            AddChild(rankLabel);

            AddChild(new Background());
        }

        private partial class Background : Control
        {
            public Background()
            {
                SetDrawBehindParent(true);
            }
            public override void _Draw()
            {
                DrawStyleBox(new StyleBoxFlat
                {
                    BgColor = Colors.White.Darkened(0.8f) with { A = 0.3f},
                    Skew = new Vector2(0.3f,0)
                }, new Rect2(new Vector2(-100,0), new Vector2(1200,150)));
            }
        }

    }

}
