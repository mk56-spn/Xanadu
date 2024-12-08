// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Godot;

namespace XanaduProject.Tests
{
	[GlobalClass]
	public partial class InputTest : CenterContainer
	{
		private readonly List<InputRect> addKeys = new List<InputRect>
		{
			new InputRect
			{
				Position = new Vector2(400, 400),
				Key = "main",
				ColourOn = Colors.Yellow
			},
			new InputRect
			{
				Position = new Vector2(400, 400),
				Key = "R1",
				ColourOn = Colors.Green
			},
			new InputRect
			{
				Position = new Vector2(400, 400),
				Key = "R2",
				ColourOn = Colors.Orange
			}
		};

		public InputTest()
		{
			var container = new HBoxContainer();
			AddChild(container);

			foreach (var child in addKeys)
				container.AddChild(child);
		}

		private ColorRect inputBox()
		{
			return new ColorRect { Size = new Vector2(100, 100) };
		}

		private partial class InputRect : ColorRect
		{
			public InputRect()
			{
				CustomMinimumSize = new Vector2(100, 100);
			}

			public Color ColourOn { get; set; }
			public string? Key { get; set; }

			public override void _Ready()
			{
				base._Ready();

				AddChild(new Label
				{
					Text = Key,
					LayoutMode = 1,
					AnchorsPreset = 8
				});
			}

			public override void _Process(double delta)
			{
				base._Process(delta);
				Color = Input.IsActionPressed(Key) ? ColourOn : Colors.Black;
			}
		}
	}
}
