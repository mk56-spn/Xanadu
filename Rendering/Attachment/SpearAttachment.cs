// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using Godot;
using XanaduProject.Tools;

namespace XanaduProject.Rendering.Attachment
{
	public partial class SpearAttachment(RenderMaster renderMaster) : Node
	{
		private float swingDuration = 0.2f;

		public override void _Process(double delta)
		{
			if (Input.IsActionJustPressed("R1"))
				makeSpear(true);
			if (Input.IsActionJustPressed("R2"))
				makeSpear(false);
		}

		private void makeSpear(bool inverted)
		{
			var spear = GD.Load<PackedScene>("uid://bs4b6210ur0n0").Instantiate<Node2D>();

			renderMaster.AddChild(spear);

			var v = GetTree().CreateTween();
			spear.RotationDegrees = inverted ? 180 : 0;





			GetTree().CreateTimer(swingDuration + 0.8).Timeout
				+= spear.QueueFree;
		}
	}
}
