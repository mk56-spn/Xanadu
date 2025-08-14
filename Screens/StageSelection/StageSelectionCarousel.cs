// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Godot;

namespace XanaduProject.Screens.StageSelection
{
	public partial class StageSelectionCarousel : ScrollContainer
	{
		private readonly StageSelection stageSelection;


		private const double transition = 0.5;

		private Tween? opacityTween;

		public StageSelectionCarousel(StageSelection stageSelection)
		{
			this.stageSelection = stageSelection;
			CustomMinimumSize = new Vector2(0, 300);
			ClipContents = false;
			LayoutMode = 1;
			AnchorsPreset = 8;
			GrowHorizontal = GrowDirection.Both;
			GrowVertical = GrowDirection.Both;
			HorizontalScrollMode = ScrollMode.ShowNever;
			VerticalScrollMode = ScrollMode.ShowNever;
		}

		private HBoxContainer trackList = new();

		public override void _Ready()
		{
			base._Ready();

			trackList.AddThemeConstantOverride("separation", 100);

			var dir = DirAccess.Open("res://Stages/");

			foreach (string? level in dir.GetFiles())
			{
				GD.Print("something");
				trackList.AddChild(new StageSelectionPanel(level));
			}

			base._Ready();
			Tween? scrollTween = null;

			foreach (var panel in trackList.GetChildren().OfType<StageSelectionPanel>())
				panel.FocusEntered += () =>
				{
					scrollTween = CreateTween();
					scrollTween.TweenProperty(this, "scroll_horizontal", panel.Position.X + panel.Size.X / 2,
							transition)
						.SetTrans(Tween.TransitionType.Sine)
						.SetEase(Tween.EaseType.Out);

					stageSelection.Level = panel.Level;

					updateOpacity(panel.GetIndex());
				};

			AddChild(trackList);
			trackList.GetChild<StageSelectionPanel>(0).GrabFocus();
		}

		private void updateOpacity(int focusedIndex)
		{
			foreach (var panel in trackList.GetChildren().OfType<StageSelectionPanel>())
			{
				var alpha = panel.Modulate;
				alpha.A = 1f / (1 + Math.Abs(panel.GetIndex() - focusedIndex));

				opacityTween = CreateTween();
				opacityTween.TweenProperty(panel, "modulate", alpha, transition)
					.SetTrans(Tween.TransitionType.Sine)
					.SetEase(Tween.EaseType.Out);
			}
		}
	}
}
