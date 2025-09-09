// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Friflo.Engine.ECS;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.ECSComponents;
using XanaduProject.ECSComponents.Tag;
using XanaduProject.Factories;

namespace XanaduProject.Screens.Result
{
    public partial class ResultAccuracy : VBoxContainer
    {

        private ScoreCalculator score;
        private Label accuracyLabel = new()
        {
            LabelSettings = new LabelSettings
            {
                FontSize = 100,
                Font = FontSource.PLASTIC
            }
        };
        public ResultAccuracy(ScoreCalculator score)
        {
            this.score = score;

            calculateAccuracy();
            calculateRank();
            AddChild(accuracyLabel);
        }

        private void calculateAccuracy()
        {

            accuracyLabel.Text = score.Accuracy.ToString("0.00") + "%";

        }

        private void calculateRank()
        {
            var rankLabel = new Label
            {
                Text = score.Accuracy switch
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
                    FontSize = 150,
                    OutlineColor = Colors.Gold,
                    OutlineSize = 15,
                    Font = new FontVariation
                    {
                        BaseFont = FontSource.PLASTIC
                    }
                },
                Position = new Vector2(0, 180),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            rankLabel.Modulate = score.Accuracy switch
            {
                >= 99.9f => new Color(0.9f, 0.9f, 1.0f), // Platinum
                >= 99f => Colors.Gold,
                >= 98f => Colors.Silver,
                >= 95f => new Color(0.8f, 0.5f, 0.2f), // Bronze
                _ => Colors.White
            };

            AddChild(rankLabel);

        }

        private static readonly GradientTexture1D gradient = new()
        {
            Gradient = new Gradient {
                Colors = [Colors.Gold.Darkened(0.1f), Colors.Black],
            }
        };
        public override void _Draw()
        {
            Vector2 v = Size + new Vector2(400, 0);
            DrawRect( new Rect2(new Vector2(-50,0),v), Colors.Black with { A = 0.3f});

            for (int i = 0; i < 5; i++)
            {
                DrawMesh(MeshFactory.CreateArrow(200),null, new Transform2D(float.Pi / 2 , new Vector2(i * 150 + 100, v.Y / 2))
                    .ScaledLocal(new Vector2(1 - i * 0.05f, 1 - i * 0.05f)),Colors.Gold with { A = 0.3f});
            }
            DrawRect( new Rect2(new Vector2(-50,0),v),Colors.Gold, false, 4, antialiased: false);
        }
        public override void _Ready()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft);
        }
    }
}
