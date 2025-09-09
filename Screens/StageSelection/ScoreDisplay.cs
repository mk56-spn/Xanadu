// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using Godot;
using XanaduProject.DataStructure;
using XanaduProject.Factories;
using XanaduProject.Tools;

namespace XanaduProject.Screens.StageSelection
{
    public partial class ScoreDisplay : VBoxContainer
    {        public void Update(StageInfo stageInfo)
        {
            // Clear previous scores
            foreach (var child in GetChildren()) child.QueueFree();

            AddChild(new Label { Text = "Scores" });

            if (GameSettings.CurrentProfile == null)
            {
                AddChild(new Label { Text = "No profile selected." });
                return;
            }

            var stageScores = GameSettings.CurrentProfile.Scores
                .Where(s => s.StagePath == stageInfo.StagePath)
                .OrderByDescending(s => s.Timestamp)
                .ToList();

            if (stageScores.Count != 0)
            {
                float delay = 0f; // Initialize delay
                foreach (var score in stageScores)
                {
                    string rank = score.Accuracy > 0.98f ? "S" : score.Accuracy > 0.94f ? "A" : score.Accuracy > 0.90f ? "B" : "C";

                    Label label;
                    AddChild(label = new Label
                    {
                        LabelSettings = new LabelSettings { FontSize = 20, Font = FontSource.LINE_BOLD },
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        CustomMinimumSize = new Vector2(400,50),
                        SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                        Text = $"Rank: {rank} | Combo: {score.MaxCombo} | Acc: {score.Accuracy:P2}"
                    });

                    // Animation: Fade in the label with a delay
                    label.Modulate = new Color(1, 1, 1, 0); // Start fully transparent
                    var tween = GetTree().CreateTween();
                    tween.TweenProperty(label, "modulate", new Color(1, 1, 1, 1), 0.5f)
                         .SetTrans(Tween.TransitionType.Quad)
                         .SetEase(Tween.EaseType.Out)
                         .SetDelay(delay); // Apply delay
                    delay += 0.1f; // Increment delay for the next label

                    var canvas = RenderRid.Create(label)
                        .SetDrawBehindParent();

                    label.Draw += () => canvas.AddSquareWithInsets(new Rect2(Vector2.Zero, label.Size), new Inset(),new Bevel
                        {
                            BevelRadius = 10,
                            BevelSides = Sides.BottomRight
                        },
                        new ShapeStyle
                        {
                            FillColor = UiColours.GREY2,
                            OutlineColor = UiColours.GREY1,
                            OutlineWidth = 1,
                            Outline = true,
                        });
                }
            }
            else
            {
                AddChild(new Label { Text = "No scores for this stage yet." });
            }
        }
    }
}
